export function scrollToBottom(element) {
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
}

export function initResize(panel, handle) {
    if (!panel || !handle) return;

    let isResizing = false;
    let startX, startY, startW, startH;

    handle.addEventListener('mousedown', (e) => {
        isResizing = true;
        startX = e.clientX;
        startY = e.clientY;
        startW = panel.offsetWidth;
        startH = panel.offsetHeight;
        document.body.style.userSelect = 'none';
        document.body.style.cursor = 'nw-resize';
        e.preventDefault();
        e.stopPropagation();
    });

    document.addEventListener('mousemove', (e) => {
        if (!isResizing) return;
        const newW = Math.max(320, Math.min(startW - (e.clientX - startX), window.innerWidth - 48));
        const newH = Math.max(400, Math.min(startH - (e.clientY - startY), window.innerHeight - 48));
        panel.style.width = newW + 'px';
        panel.style.height = newH + 'px';
        e.preventDefault();
    });

    document.addEventListener('mouseup', () => {
        if (isResizing) {
            isResizing = false;
            document.body.style.userSelect = '';
            document.body.style.cursor = '';
        }
    });
}
