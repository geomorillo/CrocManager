// ─── Globals ───
let activeTab = 'send';

// ─── Toast ───
function showToast(message, type = 'info') {
  const t = document.getElementById('toast');
  t.textContent = message;
  t.className = `toast ${type} show`;
  setTimeout(() => t.classList.remove('show'), 4000);
}

// ─── File helpers ───
function getSelectedFiles() {
  const el = document.getElementById('sendFiles');
  return el.value ? el.value.split(';;').filter(Boolean) : [];
}

function updateSendFileList() {
  const files = getSelectedFiles();
  const list = document.getElementById('sendFileList');
  const count = document.getElementById('sendFileCount');
  list.innerHTML = '';
  if (files.length === 0) { count.textContent = ''; return; }
  files.forEach(f => {
    const li = document.createElement('li');
    const name = f.split(/[/\\]/).pop() || f;
    const isFolder = f.endsWith('\\') || f.endsWith('/');
    li.textContent = isFolder ? `📂 ${name}/` : `📄 ${name}`;
    li.title = f;
    list.appendChild(li);
  });
  count.textContent = `📦 ${files.length} elemento(s) seleccionado(s)`;
}

function clearSendFiles() {
  document.getElementById('sendFiles').value = '';
  document.getElementById('sendFileList').innerHTML = '';
  document.getElementById('sendFileCount').textContent = '';
}

// ─── Progress handlers (called from C#) ───
function onProgress(data) {
  const fill = activeTab === 'send'
    ? document.getElementById('sendProgressFill')
    : document.getElementById('receiveProgressFill');
  const text = activeTab === 'send'
    ? document.getElementById('sendProgressText')
    : document.getElementById('receiveProgressText');

  if (!fill) return;
  fill.style.width = `${data.percent}%`;
  if (text && data.extra) {
    text.textContent = `${data.percent}% · ${data.extra}`;
  } else if (text) {
    text.textContent = `${data.percent}%`;
  }
}

function onCodeReady(data) {
  const fill = document.getElementById('sendProgressFill');
  const text = document.getElementById('sendProgressText');
  if (fill) fill.style.width = '10%';
  if (text) text.textContent = '⏳ Esperando que el receptor se conecte...';
  document.getElementById('sendCodeDisplay').textContent = data.code;
  document.getElementById('receiveCommand').textContent = `croc ${data.code}`;
  document.getElementById('sendCodeBox').style.display = 'block';
}

function onTransferComplete(data) {
  const fill = document.getElementById('sendProgressFill');
  const text = document.getElementById('sendProgressText');
  if (fill) fill.style.width = '100%';
  if (text) text.textContent = '✅ Transferencia completada';
  showToast('✅ Transferencia completada', 'success');
}

// ─── Init ───
document.addEventListener('DOMContentLoaded', async () => {
  // Check installation
  const raw = await window.WebDesktop.invoke('checkInstall', '');
  const result = typeof raw === 'string' ? JSON.parse(raw) : raw;
  const statusEl = document.getElementById('installStatus');
  const statusBar = document.getElementById('statusBar');
  if (result.success) {
    statusEl.textContent = `✅ croc ${result.version} instalado`;
    statusBar.className = 'status-bar ok';
    document.getElementById('btnSend').disabled = false;
    document.getElementById('btnReceive').disabled = false;
    document.getElementById('btnSendText').disabled = false;
  } else {
    statusEl.textContent = '❌ croc no está instalado';
    statusBar.className = 'status-bar err';
    showToast('croc no está instalado. Usa: winget install schollz.croc', 'error');
  }

  // Tabs
  document.querySelectorAll('.tab').forEach(tab => {
    tab.addEventListener('click', () => {
      document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
      document.querySelectorAll('.tab-content').forEach(tc => tc.classList.remove('active'));
      tab.classList.add('active');
      activeTab = tab.dataset.tab;
      document.getElementById(`tab-${activeTab}`).classList.add('active');
      if (activeTab === 'history') loadHistory();
    });
  });
});

// ═══ SEND ════════════════════════════════════════════════

async function browseSendFiles() {
  const raw = await window.WebDesktop.invoke('__dialog.openFile', {
    filter: 'All files (*.*)|*.*',
    multi: true
  });
  const result = typeof raw === 'string' ? JSON.parse(raw) : raw;
  if (result?.ok && result?.files?.length > 0) {
    const existing = getSelectedFiles();
    const all = [...existing, ...result.files];
    document.getElementById('sendFiles').value = [...new Set(all)].join(';;');
    updateSendFileList();
  }
}

async function browseSendFolder() {
  const raw = await window.WebDesktop.invoke('__dialog.selectFolder', {});
  const result = typeof raw === 'string' ? JSON.parse(raw) : raw;
  if (result?.ok && result?.path) {
    const existing = getSelectedFiles();
    const folder = result.path.endsWith('\\') ? result.path : result.path + '\\';
    const all = [...existing, folder];
    document.getElementById('sendFiles').value = [...new Set(all)].join(';;');
    updateSendFileList();
  }
}

async function doSend() {
  const paths = getSelectedFiles();
  if (paths.length === 0) { showToast('Selecciona al menos un archivo', 'error'); return; }

  const code = document.getElementById('sendCode').value.trim() || null;
  const btn = document.getElementById('btnSend');
  const progressDiv = document.getElementById('sendProgress');
  const progressFill = document.getElementById('sendProgressFill');
  const progressText = document.getElementById('sendProgressText');
  const codeBox = document.getElementById('sendCodeBox');

  btn.disabled = true;
  btn.textContent = '⏳ Enviando...';
  codeBox.style.display = 'none';
  progressDiv.style.display = 'block';
  progressFill.style.width = '5%';
  progressText.textContent = 'Iniciando...';

  try {
    const raw = await window.WebDesktop.invoke('sendFiles', { paths, code });
    const result = typeof raw === 'string' ? JSON.parse(raw) : raw;

    if (result.success) {
      showToast('¡Código listo! Compártelo con el receptor', 'success');
    } else {
      progressDiv.style.display = 'none';
      showToast(`Error: ${result.error}`, 'error');
    }
  } catch (err) {
    progressDiv.style.display = 'none';
    showToast(`Error: ${err.message}`, 'error');
  } finally {
    btn.disabled = false;
    btn.textContent = '🚀 Enviar';
  }
}

function copyCode() {
  const code = document.getElementById('sendCodeDisplay').textContent;
  navigator.clipboard.writeText(code).then(() =>
    showToast('Código copiado al portapapeles', 'success'));
}

// ═══ RECEIVE ═════════════════════════════════════════════

async function browseReceiveDest() {
  const raw = await window.WebDesktop.invoke('__dialog.selectFolder', {});
  const result = typeof raw === 'string' ? JSON.parse(raw) : raw;
  if (result?.ok && result?.path) {
    document.getElementById('receiveDest').value = result.path;
  }
}

async function doReceive() {
  const code = document.getElementById('receiveCode').value.trim();
  if (!code) { showToast('Ingresa el código de transferencia', 'error'); return; }

  const destination = document.getElementById('receiveDest').value.trim();
  if (!destination) { showToast('Selecciona la carpeta de destino', 'error'); return; }

  const btn = document.getElementById('btnReceive');
  const resultDiv = document.getElementById('receiveResult');
  const progressDiv = document.getElementById('receiveProgress');
  const progressFill = document.getElementById('receiveProgressFill');
  const progressText = document.getElementById('receiveProgressText');

  btn.disabled = true;
  btn.textContent = '⏳ Recibiendo...';
  resultDiv.style.display = 'none';
  progressDiv.style.display = 'block';
  progressFill.style.width = '5%';
  progressText.textContent = 'Conectando...';

  try {
    const result = JSON.parse(await window.WebDesktop.invoke('receive', { code, destination }));

    if (result.success) {
      progressFill.style.width = '100%';
      progressText.textContent = '✅ Transferencia completada';
      resultDiv.textContent = `✅ Archivos recibidos en: ${result.destination}`;
      resultDiv.style.display = 'block';
      showToast('¡Archivos recibidos!', 'success');
    } else {
      progressDiv.style.display = 'none';
      showToast(`Error: ${result.error}`, 'error');
    }
  } catch (err) {
    progressDiv.style.display = 'none';
    showToast(`Error: ${err.message}`, 'error');
  } finally {
    btn.disabled = false;
    btn.textContent = '📥 Recibir';
  }
}

// Enter key to trigger receive
document.getElementById('receiveCode').addEventListener('keydown', (e) => {
  if (e.key === 'Enter') doReceive();
});

// ═══ SEND TEXT ═══════════════════════════════════════════

async function doSendText() {
  const text = document.getElementById('textContent').value.trim();
  if (!text) { showToast('Escribe el texto a enviar', 'error'); return; }

  const code = document.getElementById('textCode').value.trim() || null;
  const btn = document.getElementById('btnSendText');
  const resultDiv = document.getElementById('textResult');

  btn.disabled = true;
  btn.textContent = '⏳ Enviando...';
  resultDiv.style.display = 'none';

  try {
    const result = JSON.parse(await window.WebDesktop.invoke('sendText', { text, code }));

    if (result.success) {
      document.getElementById('textCodeDisplay').textContent = result.code;
      document.getElementById('textReceiveCommand').textContent = `croc ${result.code}`;
      resultDiv.style.display = 'block';
      showToast('¡Texto enviado!', 'success');
    } else {
      showToast(`Error: ${result.error}`, 'error');
    }
  } catch (err) {
    showToast(`Error: ${err.message}`, 'error');
  } finally {
    btn.disabled = false;
    btn.textContent = '📤 Enviar texto';
  }
}

function copyTextCode() {
  const code = document.getElementById('textCodeDisplay').textContent;
  navigator.clipboard.writeText(code).then(() =>
    showToast('Código copiado al portapapeles', 'success'));
}

// ═══ HISTORY ═════════════════════════════════════════════

async function loadHistory() {
  const container = document.getElementById('historyList');
  container.innerHTML = '<p class="empty-state">Cargando...</p>';

  try {
    const result = JSON.parse(await window.WebDesktop.invoke('getHistory', ''));
    if (!result.success || !result.entries || result.entries.length === 0) {
      container.innerHTML = '<p class="empty-state">No hay transferencias aún</p>';
      return;
    }

    container.innerHTML = '';
    result.entries.forEach(entry => {
      const div = document.createElement('div');
      div.className = 'history-item';

      const icons = { send: '🚀', receive: '📥', text: '📝' };
      const typeLabels = { send: 'Enviado', receive: 'Recibido', text: 'Texto' };

      let fileSummary = '';
      if (entry.type === 'send' && entry.files?.length) {
        fileSummary = entry.files.map(f => f.split(/[/\\]/).pop()).join(', ');
      }
      if (entry.type === 'receive' && entry.destination) {
        fileSummary = `→ ${entry.destination}`;
      }
      if (entry.type === 'text' && entry.text_content) {
        fileSummary = `"${entry.text_content.substring(0, 60)}${entry.text_content.length > 60 ? '...' : ''}"`;
      }

      div.innerHTML = `
        <span class="history-icon">${icons[entry.type] || '📄'}</span>
        <div class="history-body">
          <div class="history-type">${typeLabels[entry.type] || entry.type}</div>
          <div class="history-code">${entry.code || '-'}</div>
          <div class="history-files" title="${fileSummary}">${fileSummary || ''}</div>
          <div class="history-time">${entry.time_ago}</div>
        </div>
        <span class="history-status ${entry.status}">${entry.status}</span>
      `;
      container.appendChild(div);
    });
  } catch (err) {
    container.innerHTML = `<p class="empty-state">Error al cargar historial: ${err.message}</p>`;
  }
}

async function doClearHistory() {
  if (!confirm('¿Limpiar todo el historial?')) return;
  await window.WebDesktop.invoke('clearHistory', '');
  loadHistory();
  showToast('Historial limpiado', 'info');
}
