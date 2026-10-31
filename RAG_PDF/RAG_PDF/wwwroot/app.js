'use strict';

const fileInput = document.getElementById('pdfFile');
const dropZone = document.getElementById('dropZone');
const uploadStatus = document.getElementById('uploadStatus');
const documentsStatus = document.getElementById('documentsStatus');
const documentsList = document.getElementById('documentsList');
const documentCount = document.getElementById('documentCount');
const refreshButton = document.getElementById('refreshDocuments');
const questionForm = document.getElementById('questionForm');
const questionInput = document.getElementById('question');
const askButton = document.getElementById('askBtn');
const clearButton = document.getElementById('clearChat');
const conversation = document.getElementById('conversation');
const messages = document.getElementById('messages');
const welcome = document.getElementById('welcome');
let uploading = false;
let asking = false;
let refreshSequence = 0;

function svgIcon(name) {
    const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    const use = document.createElementNS('http://www.w3.org/2000/svg', 'use');
    use.setAttribute('href', `#icon-${name}`);
    svg.setAttribute('aria-hidden', 'true');
    svg.append(use);
    return svg;
}

async function responseError(response) {
    if (response.status >= 500) return `Server error (${response.status}). Check the application logs.`;
    const message = (await response.text()).trim();
    return message && message.length < 300 && !message.startsWith('<') ? message : `Request failed (${response.status}).`;
}

async function loadDocuments() {
    const sequence = ++refreshSequence;
    refreshButton.disabled = true;
    try {
        const response = await fetch('/documents', { cache: 'no-store' });
        if (!response.ok) throw new Error(await responseError(response));
        const names = await response.json();
        if (sequence !== refreshSequence) return;
        documentsList.replaceChildren();
        documentCount.textContent = names.length;
        documentsStatus.textContent = names.length ? '' : 'No documents yet. Add a PDF to get started.';
        for (const name of names) {
            const item = document.createElement('li');
            item.className = 'document-item';
            const icon = document.createElement('span');
            icon.className = 'document-icon';
            icon.append(svgIcon('file'));
            const details = document.createElement('span');
            details.className = 'document-details';
            const title = document.createElement('strong');
            title.textContent = name;
            title.title = name;
            const subtitle = document.createElement('small');
            subtitle.textContent = 'Indexed document';
            details.append(title, subtitle);
            const indicator = document.createElement('span');
            indicator.className = 'document-ready';
            indicator.title = 'Indexed';
            item.append(icon, details, indicator);
            documentsList.append(item);
        }
    } catch (error) {
        if (sequence !== refreshSequence) return;
        documentsList.replaceChildren();
        documentCount.textContent = '—';
        documentsStatus.textContent = `Could not load documents. ${error.message}`;
    } finally {
        if (sequence === refreshSequence) refreshButton.disabled = false;
    }
}

async function uploadFiles(files) {
    if (uploading || !files.length) return;
    uploading = true;
    fileInput.disabled = true;
    dropZone.classList.add('is-disabled');
    const results = [];
    try {
        for (const [index, file] of Array.from(files).entries()) {
            if (!file.name.toLowerCase().endsWith('.pdf')) {
                results.push(`${file.name}: select a PDF file.`);
                continue;
            }
            uploadStatus.className = 'status-message';
            uploadStatus.textContent = `Indexing ${index + 1} of ${files.length}: ${file.name}…`;
            const form = new FormData();
            form.append('file', file);
            try {
                const response = await fetch('/upload', { method: 'POST', body: form });
                if (!response.ok) throw new Error(await responseError(response));
                results.push(`${file.name}: indexed.`);
            } catch (error) {
                results.push(`${file.name}: ${error.message}`);
            }
        }
        uploadStatus.textContent = results.join('\n');
        uploadStatus.className = results.every(result => result.endsWith(': indexed.')) ? 'status-message success' : 'status-message error';
    } finally {
        fileInput.value = '';
        fileInput.disabled = false;
        dropZone.classList.remove('is-disabled');
        uploading = false;
        await loadDocuments();
    }
}

function addMessage(role, text) {
    welcome.hidden = true;
    clearButton.disabled = false;
    const item = document.createElement('article');
    item.className = `message ${role}`;
    const avatar = document.createElement('span');
    avatar.className = 'message-avatar';
    if (role === 'assistant') avatar.append(svgIcon('spark'));
    else avatar.textContent = 'Y';
    const body = document.createElement('div');
    body.className = 'message-body';
    const label = document.createElement('strong');
    label.textContent = role === 'assistant' ? 'Papertrail' : 'You';
    const content = document.createElement('p');
    content.textContent = text;
    body.append(label, content);
    item.append(avatar, body);
    messages.append(item);
    conversation.scrollTop = conversation.scrollHeight;
    return content;
}

async function askQuestion(question) {
    if (asking || !question.trim()) return;
    asking = true;
    askButton.disabled = true;
    clearButton.disabled = true;
    addMessage('user', question);
    clearButton.disabled = true;
    questionInput.value = '';
    questionInput.style.height = '';
    const answer = addMessage('assistant', 'Searching your documents and thinking…');
    clearButton.disabled = true;
    try {
        const response = await fetch('/ask', {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ question })
        });
        if (!response.ok) throw new Error(await responseError(response));
        const data = await response.json();
        answer.textContent = data.answer || 'No answer was returned.';
    } catch (error) {
        answer.textContent = `Could not get an answer. ${error.message}`;
        answer.classList.add('message-error');
    } finally {
        asking = false;
        askButton.disabled = false;
        clearButton.disabled = false;
        conversation.scrollTop = conversation.scrollHeight;
        questionInput.focus();
    }
}

fileInput.addEventListener('change', () => uploadFiles(fileInput.files));
dropZone.addEventListener('keydown', event => {
    if ((event.key === 'Enter' || event.key === ' ') && !uploading) { event.preventDefault(); fileInput.click(); }
});
for (const eventName of ['dragenter', 'dragover']) {
    dropZone.addEventListener(eventName, event => { event.preventDefault(); if (!uploading) dropZone.classList.add('drag-over'); });
}
for (const eventName of ['dragleave', 'drop']) {
    dropZone.addEventListener(eventName, event => { event.preventDefault(); dropZone.classList.remove('drag-over'); });
}
dropZone.addEventListener('drop', event => uploadFiles(event.dataTransfer.files));
refreshButton.addEventListener('click', loadDocuments);
questionForm.addEventListener('submit', event => { event.preventDefault(); askQuestion(questionInput.value.trim()); });
questionInput.addEventListener('keydown', event => {
    if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) { event.preventDefault(); questionForm.requestSubmit(); }
});
questionInput.addEventListener('input', () => { questionInput.style.height = 'auto'; questionInput.style.height = `${Math.min(questionInput.scrollHeight, 160)}px`; });
document.querySelectorAll('[data-question]').forEach(button => button.addEventListener('click', () => askQuestion(button.dataset.question)));
clearButton.addEventListener('click', () => { messages.replaceChildren(); welcome.hidden = false; clearButton.disabled = true; questionInput.focus(); });
loadDocuments();
