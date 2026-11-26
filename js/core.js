function updateOutputFileNames() {
    var grid = document.getElementById('gvReports');
    if (!grid) return;

    var rows = grid.getElementsByTagName('tr');

    for (var i = 1; i < rows.length; i++) {
        var cells = rows[i].cells;
        if (cells.length < 5) continue;

        var filenameElement = cells[4].querySelector('.output-filename');
        if (!filenameElement) continue;

        var filename = filenameElement.getAttribute('data-filename') || '';
        var items = cells[3].querySelectorAll('.condition-item');
        var prefix = '';

        if (items.length === 1) {
            // 単一条件：基準日の選択値を表示
            var ddl = items[0].querySelector('select');

            if (ddl) {
                prefix = ddl.value;
            }
        } else {
            // 複数条件：必須項目名を角括弧で表示
            for (var j = 0; j < items.length; j++) {
                var label = items[j].querySelector('.condition-label');
                if (!label) continue;

                if (label.textContent.indexOf('*') === -1) continue;

                var name = label.textContent.trim().replace(/\s*\*$/, '');

                prefix += '[' + name + ']';
            }
        }

        filenameElement.textContent = prefix + filename;
    }
}

function updateSelectionSummary() {
    var grid = document.getElementById('gvReports');
    var summary = document.getElementById('selectionSummary');
    var rows = grid.getElementsByTagName('tr');
    var selected = [];

    for (var i = 1; i < rows.length; i++) {
        var row = rows[i];
        var cells = row.cells;

        if (cells.length < 4) continue;

        var checkbox = cells[1].querySelector('input[type="checkbox"]');
        var checked = checkbox && checkbox.checked;

        if (checked) {
            row.classList.add('selected-row');
        } else {
            row.classList.remove('selected-row');
        }

        if (!checked) continue;

        var title = cells[2].textContent.trim();
        var conditions = [];
        var items = cells[3].querySelectorAll('.condition-item');

        for (var j = 0; j < items.length; j++) {
            var item = items[j];
            var label = item.querySelector('.condition-label');
            var control = item.querySelector('select, input[type="text"], input[type="tel"]');

            if (!label || !control) continue;

            var name = label.textContent.trim().replace(/\s*\*$/, '');
            var value = control.value;

            if (control.tagName.toLowerCase() === 'select') {
                value = control.options[control.selectedIndex].text;

                if (control.value === '') {
                    value = '';
                }
            }

            conditions.push(name + '：' + value);
        }

        selected.push({ title: title, conditions: conditions });
    }

    var btnDownload = document.getElementById('btnDownload');
    if (selected.length === 0) {
        summary.textContent = 'ダウンロードする帳票を選択してください。';
        btnDownload.disabled = true;
    } else if (selected.length === 1) {
        summary.textContent = '選択帳票：' + selected[0].title + '（' + selected[0].conditions.join('、') + '）';
        btnDownload.disabled = false;
    } else {
        summary.textContent = '選択帳票：' + selected.length + '件';
        btnDownload.disabled = false;
    }
}

var downloading = false;
var downloadTimer = null;

function preventDoubleDownload() {
    if (downloading) return false;

    var msg = document.getElementById('lblMessage');

    if (msg) {
        msg.textContent = '';
        msg.className = 'message';
    }

    downloading = true;

    document.getElementById('btnDownload').classList.add('downloading');
    document.getElementById('btnDownload').value = '帳票作成中...';
    document.getElementById('btnExecProc').disabled = true;

    return true;
}

function startDownloads(batchId, tokens) {
    downloading = true;

    document.getElementById('btnDownload').classList.add('downloading');
    document.getElementById('btnDownload').value = 'ダウンロード中...';

    var index = 0;

    function downloadNext() {
        // 全件終了
        if (index >= tokens.length) {
            finishDownload();
            return;
        }

        var token = tokens[index];
        var cookieName = 'download_' + token;
        var startTime = new Date().getTime();

        // ダウンロード用iframe
        var iframe = document.createElement('iframe');
        iframe.style.display = 'none';
        iframe.src = 'Download.ashx?batch=' + encodeURIComponent(batchId) + '&token=' + encodeURIComponent(token);
        document.body.appendChild(iframe);

        // ダウンロードレスポンスの確認
        downloadTimer = setInterval(function () {
            var status = getCookie(cookieName);

            if (status === 'ready') {
                clearInterval(downloadTimer);
                downloadTimer = null;

                deleteCookie(cookieName);
                index++;
                downloadNext();
            } else if (status === 'error') {
                deleteCookie(cookieName);
                finishDownload('ダウンロードに失敗しました。');
            } else if (
                new Date().getTime() - startTime > 60000) {
                finishDownload('ダウンロードの応答がタイムアウトしました。');
            }
        }, 1000);
    }

    downloadNext();
}

function finishDownload(message) {
    clearInterval(downloadTimer);
    downloadTimer = null;

    downloading = false;

    document.getElementById('btnDownload').classList.remove('downloading');
    document.getElementById('btnDownload').value = 'ダウンロード';
    document.getElementById('btnExecProc').disabled = false;

    if (message) {
        var msg = document.getElementById('lblMessage');

        msg.textContent = message;
        msg.className = 'message error';
    }
}

function getCookie(name) {
    var cookies = document.cookie.split(';');

    for (var i = 0; i < cookies.length; i++) {
        var cookie = cookies[i].trim();

        if (cookie.indexOf(name + '=') === 0) {
            return cookie.substring(name.length + 1);
        }
    }

    return '';
}

function deleteCookie(name) {
    document.cookie = name + '=; path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT';
}

document.addEventListener('DOMContentLoaded', function () {
    var grid = document.getElementById('gvReports');

    if (!grid) return;

    grid.addEventListener('change', function (e) {
        updateSelectionSummary();

        if (e.target.tagName.toLowerCase() === 'select') {
            updateOutputFileNames();
        }
    });

    grid.addEventListener('input', function (e) {
        if (e.target.type === 'text' || e.target.type === 'tel') {
            updateSelectionSummary();
        }
    });

    updateSelectionSummary();
    updateOutputFileNames();
});
