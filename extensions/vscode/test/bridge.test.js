'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');
const { EventEmitter } = require('node:events');
const Module = require('node:module');

test('VS Code extension: blank preflight, verification, opt-in repair, focus invalidation', async () => {
    let editorText = '';
    let activeEditor = null;
    let activeEditorChanged = () => {};
    let windowStateChanged = () => {};
    let closed = () => {};
    let socket;

    const document = {
        uri: { toString: () => 'untitled:KeyER-test' },
        languageId: 'csharp',
        version: 1,
        isClosed: false,
        isReadonly: false,
        eol: 1,
        getText: () => editorText,
        offsetAt: position => position.character,
        positionAt: offset => ({ line: 0, character: offset })
    };

    const editor = {
        document,
        selections: [{ isEmpty: true, active: { line: 0, character: 0 } }],
        options: { tabSize: 4, insertSpaces: true },
        edit: async callback => {
            callback({ replace: (_range, text) => { editorText = text; document.version++; } });
            return true;
        }
    };
    activeEditor = editor;

    const vscode = {
        EndOfLine: { LF: 1, CRLF: 2 },
        StatusBarAlignment: { Left: 1 },
        Position: class Position { constructor(line, character) { this.line = line; this.character = character; } },
        Range: class Range { constructor(start, end) { this.start = start; this.end = end; } },
        window: {
            state: { focused: true },
            get activeTextEditor() { return activeEditor; },
            createOutputChannel: () => ({ appendLine() {}, dispose() {} }),
            createStatusBarItem: () => ({ text: '', tooltip: '', show() {}, dispose() {} }),
            showInformationMessage: async () => {},
            onDidChangeActiveTextEditor: listener => { activeEditorChanged = listener; return { dispose() {} }; },
            onDidChangeWindowState: listener => { windowStateChanged = listener; return { dispose() {} }; }
        },
        workspace: {
            getConfiguration: () => ({ get: (_name, fallback) => fallback }),
            onDidCloseTextDocument: listener => { closed = listener; return { dispose() {} }; }
        },
        commands: { registerCommand: () => ({ dispose() {} }) }
    };

    class Socket extends EventEmitter {
        constructor() {
            super();
            this.destroyed = false;
            this.writable = true;
            this.messages = [];
        }
        setEncoding() { return this; }
        write(data) {
            this.messages.push(JSON.parse(data.trim()));
            return true;
        }
        destroy() {
            this.destroyed = true;
            this.writable = false;
            this.emit('close');
        }
    }

    const net = { createConnection: () => { socket = new Socket(); return socket; } };
    const originalLoad = Module._load;
    Module._load = function (name, parent, isMain) {
        if (name === 'vscode') return vscode;
        if (name === 'node:net') return net;
        return originalLoad.call(this, name, parent, isMain);
    };

    let extension;
    try {
        delete require.cache[require.resolve('../extension.js')];
        extension = require('../extension.js');
    } finally {
        Module._load = originalLoad;
    }

    async function request(id, type, payload) {
        socket.emit('data', JSON.stringify({ id, type, payload }) + '\n');
        for (let i = 0; i < 100; i++) {
            await new Promise(resolve => setImmediate(resolve));
            const response = socket.messages.find(m => m.replyTo === id);
            if (response) return response;
        }
        throw new Error('No response for request ' + id);
    }

    try {
        extension.activate({ subscriptions: [] });
        socket.emit('connect');
        assert.equal(socket.messages[0].type, 'hello');

        const p1 = await request(1, 'prepare', { sourceLength: 13 });
        assert.equal(p1.ok, true);
        assert.ok(p1.sessionId);
        assert.equal(p1.editor.language, 'csharp');

        // Editor receives synthetic keyboard input and an unexpected extra brace.
        editorText = 'Hello, мир! }\n';
        const c1 = await request(2, 'complete', {
            sessionId: p1.sessionId,
            source: 'Hello, мир!\n',
            strategy: 'repair'
        });
        assert.equal(c1.ok, true);
        assert.equal(c1.matched, true);
        assert.equal(c1.repaired, true);
        assert.equal(editorText, 'Hello, мир!\n');

        const rejected = await request(3, 'prepare', { sourceLength: 4 });
        assert.equal(rejected.ok, false);
        assert.match(rejected.error, /пустой/);

        editorText = '';
        const p2 = await request(4, 'prepare', { sourceLength: 4 });
        assert.equal(p2.ok, true);
        editorText = 'wrong';
        const c2 = await request(5, 'complete', {
            sessionId: p2.sessionId,
            source: 'abcd',
            strategy: 'verify'
        });
        assert.equal(c2.ok, true);
        assert.equal(c2.matched, false);
        assert.equal(c2.repaired, false);
        assert.equal(editorText, 'wrong');

        editorText = '';
        const p3 = await request(6, 'prepare', { sourceLength: 4 });
        assert.equal(p3.ok, true);
        activeEditor = undefined;
        activeEditorChanged();
        assert.ok(socket.messages.some(x => x.type === 'invalidated' &&
            x.sessionId === p3.sessionId));

        activeEditor = editor;
        editorText = '';
        const p4 = await request(7, 'prepare', { sourceLength: 4 });
        assert.equal(p4.ok, true);
        vscode.window.state.focused = false;
        windowStateChanged({ focused: false });
        assert.ok(socket.messages.some(x => x.type === 'invalidated' &&
            x.sessionId === p4.sessionId));

        vscode.window.state.focused = true;
        const p5 = await request(8, 'prepare', { sourceLength: 4 });
        assert.equal(p5.ok, true);
        closed(document);
        assert.ok(socket.messages.some(x => x.type === 'invalidated' &&
            x.sessionId === p5.sessionId));
    } finally {
        extension.deactivate();
    }
});
