// Bridges postMessage traffic between the Blazor WorkflowEditor host page and
// the framework-free editor prototype running inside the iframe.
window.shopfloorWorkflowBridge = (function () {
    let dotNetRef = null;
    let iframeId = null;
    let pendingLoadJson = null;

    function iframeWindow() {
        const el = document.getElementById(iframeId);
        return el ? el.contentWindow : null;
    }

    function post(message) {
        const win = iframeWindow();
        if (win) {
            win.postMessage(message, '*');
        }
    }

    function onMessage(event) {
        // The editor runs in a same-origin iframe, so only accept messages that
        // originate from this window's origin and from the editor iframe itself.
        if (event.origin !== window.location.origin) {
            return;
        }

        const frame = iframeWindow();
        if (frame && event.source !== frame) {
            return;
        }

        const data = event.data;
        if (!data || typeof data !== 'object') {
            return;
        }

        if (data.type === 'shopfloor.workflow.ready') {
            // Editor finished booting — (re)send the load payload if we have it.
            if (pendingLoadJson) {
                post(JSON.parse(pendingLoadJson));
            } else if (dotNetRef) {
                // No payload cached yet — ask the host to fetch + push it.
                dotNetRef.invokeMethodAsync('OnLoadRequested');
            }
            return;
        }

        if (data.type === 'shopfloor.workflow.request-load') {
            // Editor has no workflow — resend cached payload or ask host to fetch.
            if (pendingLoadJson) {
                post(JSON.parse(pendingLoadJson));
            } else if (dotNetRef) {
                dotNetRef.invokeMethodAsync('OnLoadRequested');
            }
            return;
        }

        if (data.type === 'shopfloor.workflow.save' && dotNetRef) {
            dotNetRef.invokeMethodAsync('OnSaveRequested', JSON.stringify(data));
            return;
        }

        if (data.type === 'shopfloor.workflow.validate' && dotNetRef) {
            dotNetRef.invokeMethodAsync('OnValidateRequested', JSON.stringify(data));
        }
    }

    return {
        init: function (ref, id) {
            dotNetRef = ref;
            iframeId = id;
            window.addEventListener('message', onMessage);
        },
        postLoad: function (loadJson) {
            pendingLoadJson = loadJson;
            post(JSON.parse(loadJson));
        },
        postSaveResult: function (resultJson) {
            post(JSON.parse(resultJson));
        },
        postValidateResult: function (resultJson) {
            post(JSON.parse(resultJson));
        },
        dispose: function () {
            window.removeEventListener('message', onMessage);
            dotNetRef = null;
            pendingLoadJson = null;
        }
    };
})();
