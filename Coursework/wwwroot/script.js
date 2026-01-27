// Quill.js interop (Rich Text Editor)
window.quillInterop = (function () {
    const editors = new Map();

    function create(elementId, readOnly) {
        if (!window.Quill) {
            console.warn("Quill is not loaded.");
            return;
        }

        const el = document.getElementById(elementId);
        if (!el) return;
        if (editors.has(elementId)) return;

        const toolbarOptions = [
            ['bold', 'italic', 'underline', 'strike'],
            [{ 'header': [1, 2, 3, false] }],
            [{ 'list': 'ordered' }, { 'list': 'bullet' }],
            ['link'],
            ['clean']
        ];

        const quill = new Quill(el, {
            theme: 'snow',
            readOnly: !!readOnly,
            modules: {
                toolbar: toolbarOptions
            }
        });

        editors.set(elementId, quill);
    }

    function setHtml(elementId, html) {
        const quill = editors.get(elementId);
        if (!quill) return;
        quill.clipboard.dangerouslyPasteHTML(html || "");
    }

    function getHtml(elementId) {
        const quill = editors.get(elementId);
        if (!quill) return "";
        return quill.root && quill.root.innerHTML ? quill.root.innerHTML : "";
    }

    function destroy(elementId) {
        // Quill doesn't provide a destroy API; removing references is enough.
        editors.delete(elementId);
    }

    return {
        create,
        setHtml,
        getHtml,
        destroy
    };
})();

// Function to initialize theme
function initializeTheme() {
    const htmlElement = document.documentElement;
    let wiredLight = null;
    let wiredDark = null;

    // Function to update theme colors and button state
    function updateTheme() {
        const isDark = htmlElement.classList.contains('dark');

        if (wiredLight && wiredDark) {
            if (isDark) {
                wiredLight.classList.remove('active');
                wiredDark.classList.add('active');
            } else {
                wiredLight.classList.add('active');
                wiredDark.classList.remove('active');
            }
        }

        // Update chart colors
        let color = '#1b3737';
        if (isDark) {
            color = '#4fd1c5';
        }

        const gradientStop1 = document.getElementById('gradientStop1');
        const gradientStop2 = document.getElementById('gradientStop2');
        const chartLine = document.getElementById('chartLine');

        if (gradientStop1) {
            gradientStop1.setAttribute('stop-color', color);
        }
        if (gradientStop2) {
            gradientStop2.setAttribute('stop-color', color);
        }
        if (chartLine) {
            chartLine.setAttribute('stroke', color);
        }
    }

    // Wire up buttons when they appear (handles layout swaps and re-renders)
    function wireButtonsIfPresent() {
        const lightButton = document.getElementById('light-mode-btn');
        const darkButton = document.getElementById('dark-mode-btn');

        const changed = lightButton !== wiredLight || darkButton !== wiredDark;
        if (!lightButton || !darkButton || !changed) {
            return;
        }

        wiredLight = lightButton;
        wiredDark = darkButton;

        // Clear existing handlers by resetting onclick
        wiredLight.onclick = null;
        wiredDark.onclick = null;

        wiredLight.addEventListener('click', function () {
            htmlElement.classList.remove('dark');
            htmlElement.classList.add('light');
            localStorage.setItem('theme', 'light');
            updateTheme();
        });

        wiredDark.addEventListener('click', function () {
            htmlElement.classList.remove('light');
            htmlElement.classList.add('dark');
            localStorage.setItem('theme', 'dark');
            updateTheme();
        });

        updateTheme();
    }

    // Load theme from localStorage on startup
    const savedTheme = localStorage.getItem('theme');
    if (savedTheme === 'dark') {
        htmlElement.classList.add('dark');
        htmlElement.classList.remove('light');
    } else {
        htmlElement.classList.add('light');
        htmlElement.classList.remove('dark');
    }
    updateTheme();

    // Initial attempt and observe future DOM changes (for layout switches)
    wireButtonsIfPresent();
    const observer = new MutationObserver(() => wireButtonsIfPresent());
    observer.observe(document.body, { childList: true, subtree: true });

    // Also try a few times shortly after load to cover delayed renders
    let retries = 0;
    const maxRetries = 20;
    const retryTimer = setInterval(() => {
        wireButtonsIfPresent();
        retries++;
        if (retries >= maxRetries || (wiredLight && wiredDark)) {
            clearInterval(retryTimer);
        }
    }, 200);
}

// Start Blazor and initialize theme
if (typeof Blazor !== 'undefined') {
    Blazor.start().then(function () {
        setTimeout(initializeTheme, 500);
    });
} else {
    // Fallback for when Blazor object is not available
    window.addEventListener('load', function () {
        setTimeout(initializeTheme, 500);
    });
}

// Additional fallback - try after a delay
setTimeout(initializeTheme, 1000);

// Download helper for Blazor: save a base64 PDF as a file
window.downloadFileFromBytes = function (fileName, base64Data) {
    try {
        const link = document.createElement('a');
        link.href = "data:application/pdf;base64," + base64Data;
        link.download = fileName || "journal-entries.pdf";
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    } catch (e) {
        console.error("downloadFileFromBytes failed", e);
        alert("Unable to start download.");
    }
};
