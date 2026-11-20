'use strict';

const vscode = require('vscode');
const net = require('node:net');
const crypto = require('node:crypto');

const PIPE = '\\\\.\\pipe\\KeyER.IdeBridge.v1';
const MAX_LINE = 2_000_000;
const RECONNECT_MS = 1600;

let socket = null;
let buffer = '';
let pendingChain = Promise.resolve();
let activeSession = null;
let disposed = false;
let reconnectTimer = null;
let output;
let status;

function setStatus(connected) {
    if (!status) return;
    status.text = connected ? '$(check) KeyER IDE' : '$(debug-disconnect) KeyER IDE';
    status.tooltip = connected
        ? 'KeyER: соединение с Windows-приложением установлено'
        : 'KeyER: нет соединения с приложением, проверьте его запуск';
}

function send(message) {
    if (!socket || socket.destroyed || !socket.writable) return false;
    const line = JSON.stringify(message);
    if (line.length > MAX_LINE) return false;
    socket.write(line + '\n', 'utf8');
    return true;
}

function reply(id, ok, result = {}) {
    send({ replyTo: id, ok, ...result });
}

function invalidate(reason) {
    if (!activeSession) return;
    const sessionId = activeSession.id;
    activeSession = null;
    send({ type: 'invalidated', sessionId, reason });
}

function currentEditor() {
    if (!vscode.window.state.focused) return undefined;
    return vscode.window.activeTextEditor;
}

function isSameSessionEditor() {
    const editor = currentEditor();
    return activeSession && editor &&
        editor.document === activeSession.document &&
        editor.document.uri.toString() === activeSession.uri;
}

function takeConfig(editor) {
    const config = vscode.workspace.getConfiguration('editor', editor.document.uri);
    return {
        language: editor.document.languageId,
        autoIndent: config.get('autoIndent', 'advanced'),
        autoClosingBrackets: config.get('autoClosingBrackets', 'languageDefined'),
        autoClosingQuotes: config.get('autoClosingQuotes', 'languageDefined'),
        formatOnType: config.get('formatOnType', false),
        formatOnPaste: config.get('formatOnPaste', false),
        acceptSuggestionOnEnter: config.get('acceptSuggestionOnEnter', 'on'),
        tabSize: editor.options.tabSize,
        insertSpaces: editor.options.insertSpaces,
        eol: editor.document.eol
    };
}

function prepare(id, payload) {
    if (activeSession) {
        reply(id, false, { error: 'Другая IDE-сессия ещё не завершена.' });
        return;
    }
    const editor = currentEditor();
    if (!editor) {
        reply(id, false, { error: 'Окно VS Code должно быть активным.' });
        return;
    }
    const document = editor.document;
    const text = document.getText();
    const selections = editor.selections;
    if (document.isClosed || document.isReadonly ||
        selections.length !== 1 || !selections[0].isEmpty ||
        document.offsetAt(selections[0].active) !== 0 || text.length !== 0) {
        reply(id, false, {
            error: 'Для проверяемого набора откройте пустой текстовый документ и поставьте один курсор в начало.'
        });
        return;
    }
    if (!payload || !Number.isSafeInteger(payload.sourceLength) ||
        payload.sourceLength <= 0 || payload.sourceLength > 250000) {
        reply(id, false, { error: 'Неверный размер исходного текста или превышен лимит 250 000 символов.' });
        return;
    }
    activeSession = {
        id: crypto.randomUUID(),
        document,
        uri: document.uri.toString(),
        version: document.version,
        startedAt: Date.now(),
        sourceLength: payload.sourceLength
    };
    reply(id, true, {
        sessionId: activeSession.id,
        editor: takeConfig(editor),
        message: 'Пустой редактор подтверждён, проверка результата включена.'
    });
}

async function complete(id, payload) {
    if (!isSameSessionEditor() || !payload || payload.sessionId !== activeSession.id) {
        invalidate('active-editor-changed');
        reply(id, false, { error: 'Активный редактор изменился, сверка отменена.' });
        return;
    }
    const session = activeSession;
    activeSession = null;

    if (typeof payload.source !== 'string' ||
        payload.source.length !== session.sourceLength ||
        payload.source.length > 250000) {
        reply(id, false, { error: 'Размер проверяемого кода не совпадает с подготовленной сессией.' });
        return;
    }
    const editor = vscode.window.activeTextEditor;
    const document = editor.document;
    const expected = payload.source.replace(/\r\n?/g, '\n');
    const actual = document.getText().replace(/\r\n?/g, '\n');
    if (actual === expected) {
        reply(id, true, { matched: true, repaired: false, length: expected.length });
        return;
    }
    if (payload.strategy !== 'repair') {
        reply(id, true, { matched: false, repaired: false, length: expected.length });
        return;
    }

    if (!vscode.window.state.focused || document !== session.document ||
        document.isReadonly) {
        reply(id, false, { error: 'Документ больше не подходит для безопасной коррекции.' });
        return;
    }

    const documentEol = document.eol === vscode.EndOfLine.CRLF ? '\r\n' : '\n';
    const fixedText = expected.replace(/\n/g, documentEol);
    const end = document.positionAt(document.getText().length);
    const applied = await editor.edit(builder => {
        builder.replace(new vscode.Range(new vscode.Position(0, 0), end), fixedText);
    }, { undoStopBefore: true, undoStopAfter: true });

    if (!applied) {
        reply(id, false, { error: 'VS Code отклонил исправление. Исходный документ не сохранён.' });
        return;
    }
    const verified = document.getText().replace(/\r\n?/g, '\n') === expected;
    reply(id, true, { matched: verified, repaired: verified, length: expected.length });
}

async function handle(message) {
    if (!message || !Number.isSafeInteger(message.id) || typeof message.type !== 'string')
        return;
    try {
        if (message.type === 'prepare') {
            prepare(message.id, message.payload);
        } else if (message.type === 'complete') {
            await complete(message.id, message.payload);
        } else if (message.type === 'cancel') {
            if (activeSession && message.payload &&
                activeSession.id === message.payload.sessionId) activeSession = null;
            reply(message.id, true);
        } else if (message.type === 'ping') {
            reply(message.id, true, { extension: 'KeyER VS Code', version: 1 });
        } else {
            reply(message.id, false, { error: 'Unknown request.' });
        }
    } catch (error) {
        output.appendLine('IDE bridge error: ' + (error && error.name ? error.name : 'unknown'));
        reply(message.id, false, { error: 'Не удалось выполнить операцию в VS Code.' });
    }
}

function connect() {
    if (disposed || socket) return;

    const connection = net.createConnection(PIPE);
    socket = connection;
    buffer = '';

    connection.on('connect', () => {
        setStatus(true);
        send({ type: 'hello', version: 1 });
    });

    connection.on('data', chunk => {
        buffer += chunk.toString('utf8');
        if (buffer.length > MAX_LINE) {
            connection.destroy();
            return;
        }
        let newline;
        while ((newline = buffer.indexOf('\n')) >= 0) {
            const line = buffer.slice(0, newline);
            buffer = buffer.slice(newline + 1);
            if (!line.trim()) continue;
            try {
                const message = JSON.parse(line);
                pendingChain = pendingChain.then(() => handle(message)).catch(error => {
                    output.appendLine('Protocol error: ' + (error && error.name ? error.name : 'unknown'));
                });
            } catch (_) {
                connection.destroy();
                return;
            }
        }
    });

    connection.on('error', error => {
        if (error.code !== 'ENOENT' && error.code !== 'ECONNREFUSED')
            output.appendLine('Connection error: ' + (error.code || 'unknown'));
    });

    connection.on('close', () => {
        if (socket === connection) socket = null;
        buffer = '';
        activeSession = null;
        setStatus(false);
        if (!disposed) reconnectTimer = setTimeout(connect, RECONNECT_MS);
    });
}

function activate(context) {
    output = vscode.window.createOutputChannel('KeyER IDE');
    status = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 30);
    setStatus(false);
    status.show();
    context.subscriptions.push(output, status);

    context.subscriptions.push(
        vscode.commands.registerCommand('keyerBridge.showStatus', () => {
            vscode.window.showInformationMessage(socket && !socket.destroyed
                ? 'KeyER подключён. Вводите код в пустой файл через глобальную клавишу KeyER.'
                : 'KeyER не подключён. Запустите KeyER и убедитесь, что права процессов совпадают.');
        }),
        vscode.window.onDidChangeActiveTextEditor(() => {
            if (activeSession && !isSameSessionEditor()) invalidate('active-editor-changed');
        }),
        vscode.window.onDidChangeWindowState(state => {
            if (!state.focused) invalidate('window-deactivated');
        }),
        vscode.workspace.onDidCloseTextDocument(document => {
            if (activeSession && activeSession.document === document)
                invalidate('document-closed');
        })
    );

    connect();
}

function deactivate() {
    disposed = true;
    clearTimeout(reconnectTimer);
    activeSession = null;
    if (socket) socket.destroy();
    socket = null;
}

module.exports = { activate, deactivate };
