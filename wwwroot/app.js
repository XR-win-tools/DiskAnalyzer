// ==================== 工具 ====================
const TOKEN = new URLSearchParams(location.search).get('t') || '';

function api(path, opts = {}) {
    const sep = path.includes('?') ? '&' : '?';
    const url = path + sep + 't=' + encodeURIComponent(TOKEN);
    if (!opts.headers) opts.headers = {};
    opts.headers['X-Token'] = TOKEN;
    return fetch(url, opts);
}

function formatSize(bytes) {
    if (!bytes || bytes === 0) return '0 B';
    const units = ['B', 'KB', 'MB', 'GB', 'TB', 'PB'];
    let i = 0;
    let size = bytes;
    while (size >= 1024 && i < units.length - 1) { size /= 1024; i++; }
    return (i === 0 ? bytes : size.toFixed(2)) + ' ' + units[i];
}

function escapeHtml(s) {
    if (s == null) return '';
    return String(s).replace(/[&<>"']/g, c => ({
        '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
    }[c]));
}

// ==================== 状态 ====================
let currentSessionId = null;
let pollTimer = null;
let isScanning = false;

// ==================== 盘符 ====================
async function loadDrives() {
    try {
        const r = await api('/api/drives');
        if (!r.ok) throw new Error('HTTP ' + r.status);
        const drives = await r.json();
        const sel = document.getElementById('drive-select');
        sel.innerHTML = '<option value="">-- 选择盘符 --</option>' +
            drives.map(d => {
                const label = d.label ? ` (${d.label})` : '';
                const free = formatSize(d.freeSpace);
                const total = formatSize(d.totalSize);
                return `<option value="${escapeHtml(d.name)}">${escapeHtml(d.name)}${escapeHtml(label)} · ${free} 可用 / ${total}</option>`;
            }).join('');
    } catch (e) {
        console.error('加载盘符失败', e);
    }
}

// ==================== 扫描 ====================
async function startScan() {
    // 若正在扫描，此按钮变为“停止”
    if (isScanning) {
        await cancelScan();
        return;
    }

    const path = document.getElementById('path-input').value.trim();
    if (!path) {
        alert('请输入要扫描的路径');
        return;
    }

    isScanning = true;
    setScanButton('停止', true);
    showProgress(true);
    document.getElementById('progress-fill').classList.add('indeterminate');
    document.getElementById('progress-fill').style.width = '';
    document.getElementById('progress-status').textContent = '正在启动扫描…';
    document.getElementById('progress-summary').textContent = '';
    document.getElementById('progress-path').textContent = '';

    try {
        const r = await api('/api/scan', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ path })
        });
        if (!r.ok) {
            const err = await r.json().catch(() => ({}));
            throw new Error(err.error || '启动扫描失败');
        }
        const data = await r.json();
        currentSessionId = data.id;
        pollTimer = setInterval(pollStatus, 200);
        await pollStatus();
    } catch (e) {
        alert(e.message || '启动扫描失败');
        endScan();
    }
}

async function pollStatus() {
    if (!currentSessionId) return;
    try {
        const r = await api('/api/scan/' + currentSessionId);
        if (!r.ok) { endScan(); return; }
        const data = await r.json();

        updateProgress(data);

        if (data.isDone) {
            endScan();
            if (data.error) {
                alert('扫描出错：' + data.error);
            } else if (data.isCancelled) {
                document.getElementById('progress-status').textContent = '已取消';
            } else if (data.result) {
                renderResult(data.result);
            }
        }
    } catch (e) {
        console.error(e);
    }
}

function updateProgress(data) {
    document.getElementById('progress-status').textContent =
        `正在扫描… 已扫描 ${data.filesScanned.toLocaleString()} 个文件`;
    document.getElementById('progress-summary').textContent =
        `累计 ${formatSize(data.totalSize)} · ${data.foldersScanned.toLocaleString()} 个文件夹`;
    document.getElementById('progress-path').textContent = data.currentPath || '';
}

async function cancelScan() {
    if (!currentSessionId) return;
    try {
        await api('/api/scan/' + currentSessionId + '/cancel', { method: 'POST' });
    } catch (e) { /* ignore */ }
    endScan();
    document.getElementById('progress-status').textContent = '已取消';
}

function endScan() {
    if (pollTimer) { clearInterval(pollTimer); pollTimer = null; }
    currentSessionId = null;
    isScanning = false;
    setScanButton('开始扫描', false);
    document.getElementById('progress-fill').classList.remove('indeterminate');
}

function setScanButton(text, isDanger) {
    const btn = document.getElementById('scan-btn');
    btn.textContent = text;
    btn.classList.toggle('btn-primary', !isDanger);
    btn.classList.toggle('btn-danger', isDanger);
}

function showProgress(visible) {
    document.getElementById('progress-section').classList.toggle('hidden', !visible);
}

// ==================== 渲染 ====================
function renderResult(result) {
    document.getElementById('progress-status').textContent =
        `扫描完成 · 用时 ${result.elapsedSeconds.toFixed(1)} 秒`;
    document.getElementById('progress-summary').textContent =
        `总计 ${formatSize(result.totalSize)} · ${result.totalFiles.toLocaleString()} 个文件`;
    document.getElementById('progress-fill').style.width = '100%';

    renderTree(result.root, result.totalSize);
    renderFiles(result.topFiles);
    renderTypes(result.fileTypes);
}

// ---------- 树 ----------
function renderTree(rootNode, totalSize) {
    const container = document.getElementById('tree-container');
    container.innerHTML = '';
    if (!rootNode) {
        container.innerHTML = '<div class="empty-state"><p>无数据</p></div>';
        return;
    }
    container.appendChild(createTreeNode(rootNode, totalSize, true));
}

function createTreeNode(node, parentSize, initiallyExpanded) {
    const wrap = document.createElement('div');
    wrap.className = 'tree-node';

    const row = document.createElement('div');
    row.className = 'tree-row';

    const hasChildren = node.children && node.children.length > 0;

    const toggle = document.createElement('span');
    toggle.className = 'tree-toggle' + (hasChildren ? '' : ' leaf');
    toggle.textContent = '▶';
    row.appendChild(toggle);

    const icon = document.createElement('span');
    icon.className = 'tree-icon';
    icon.textContent = hasChildren ? '📁' : '📂';
    row.appendChild(icon);

    const name = document.createElement('span');
    name.className = 'tree-name';
    name.textContent = node.name;
    name.title = node.fullPath;
    row.appendChild(name);

    const size = document.createElement('span');
    size.className = 'tree-size';
    size.textContent = formatSize(node.size);
    row.appendChild(size);

    const bar = document.createElement('div');
    bar.className = 'tree-bar';
    const fill = document.createElement('div');
    fill.className = 'tree-bar-fill';
    const pct = parentSize > 0 ? Math.min(100, (node.size / parentSize) * 100) : 0;
    fill.style.width = pct + '%';
    bar.appendChild(fill);
    row.appendChild(bar);

    wrap.appendChild(row);

    if (hasChildren) {
        const childrenEl = document.createElement('div');
        childrenEl.className = 'tree-children hidden';
        wrap.appendChild(childrenEl);

        const doExpand = () => {
            if (childrenEl.childElementCount === 0) {
                for (const child of node.children) {
                    childrenEl.appendChild(createTreeNode(child, node.size, false));
                }
            }
            childrenEl.classList.remove('hidden');
            toggle.classList.add('expanded');
        };

        const doCollapse = () => {
            childrenEl.classList.add('hidden');
            toggle.classList.remove('expanded');
        };

        row.addEventListener('click', (e) => {
            if (e.target === name) return; // 双击名字的时候不切换
            if (childrenEl.classList.contains('hidden')) doExpand();
            else doCollapse();
        });

        if (initiallyExpanded) doExpand();
    }

    // 双击文件夹 → 资源管理器打开
    name.addEventListener('dblclick', (e) => {
        e.stopPropagation();
        openInExplorer(node.fullPath, false);
    });

    return wrap;
}

// ---------- 最大文件 ----------
function renderFiles(files) {
    const container = document.getElementById('files-container');
    if (!files || files.length === 0) {
        container.innerHTML = '<div class="empty-state"><p>没有文件</p></div>';
        return;
    }

    container.innerHTML = '';
    const header = document.createElement('div');
    header.className = 'list-header files';
    header.innerHTML = '<div class="right">大小</div><div>文件名</div><div>所在目录</div>';
    container.appendChild(header);

    const frag = document.createDocumentFragment();
    for (const f of files) {
        const row = document.createElement('div');
        row.className = 'file-row';
        row.title = '点击在资源管理器中定位';
        row.innerHTML = `
            <div class="size">${formatSize(f.size)}</div>
            <div class="name">${escapeHtml(f.name)}</div>
            <div class="dir">${escapeHtml(f.directory)}</div>
        `;
        row.addEventListener('click', () => openInExplorer(f.fullPath, true));
        frag.appendChild(row);
    }
    container.appendChild(frag);
}

// ---------- 文件类型 ----------
function renderTypes(types) {
    const container = document.getElementById('types-container');
    if (!types || types.length === 0) {
        container.innerHTML = '<div class="empty-state"><p>没有文件</p></div>';
        return;
    }

    container.innerHTML = '';
    const header = document.createElement('div');
    header.className = 'list-header types';
    header.innerHTML = '<div>扩展名</div><div class="right">文件数</div><div class="right">总大小</div><div>占比</div>';
    container.appendChild(header);

    const frag = document.createDocumentFragment();
    for (const t of types) {
        const row = document.createElement('div');
        row.className = 'type-row';
        row.innerHTML = `
            <div class="ext" title="${escapeHtml(t.extension)}">${escapeHtml(t.extension)}</div>
            <div class="count">${t.count.toLocaleString()}</div>
            <div class="total-size">${formatSize(t.totalSize)}</div>
            <div class="type-bar">
                <div class="type-bar-fill" style="width:${Math.max(2, t.percent).toFixed(2)}%">${t.percent.toFixed(1)}%</div>
            </div>
        `;
        frag.appendChild(row);
    }
    container.appendChild(frag);
}

// ==================== 打开资源管理器 ====================
async function openInExplorer(path, reveal) {
    try {
        await api('/api/open', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ path, reveal })
        });
    } catch (e) {
        console.error(e);
    }
}

// ==================== Tabs ====================
function setupTabs() {
    document.querySelectorAll('.tab-btn').forEach(btn => {
        btn.addEventListener('click', () => {
            const tab = btn.dataset.tab;
            document.querySelectorAll('.tab-btn').forEach(b =>
                b.classList.toggle('active', b === btn));
            document.querySelectorAll('.tab-panel').forEach(p =>
                p.classList.toggle('active', p.id === 'tab-' + tab));
        });
    });
}

// ==================== 启动 ====================
document.addEventListener('DOMContentLoaded', () => {
    setupTabs();
    loadDrives();

    document.getElementById('scan-btn').addEventListener('click', startScan);
    document.getElementById('path-input').addEventListener('keydown', (e) => {
        if (e.key === 'Enter' && !isScanning) startScan();
    });

    const sel = document.getElementById('drive-select');
    sel.addEventListener('change', () => {
        if (sel.value) {
            document.getElementById('path-input').value = sel.value;
            document.getElementById('path-input').focus();
        }
    });

    // 页面关闭时若正在扫描，提示
    window.addEventListener('beforeunload', (e) => {
        if (isScanning) {
            e.preventDefault();
            e.returnValue = '';
        }
    });
});