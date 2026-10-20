// Tiny bridge between Phast's internal pages and the browser (C# side: Bridge/PageBridge.cs).
// Usage: phast.send("navigate", { text: "github.com" });  phast.on("someEvent", data => ...);
(function () {
  const handlers = {};
  const webview = window.chrome && window.chrome.webview;

  window.phast = {
    available: !!webview,
    send(type, payload) {
      if (webview) webview.postMessage(Object.assign({ type }, payload || {}));
    },
    on(type, callback) {
      (handlers[type] = handlers[type] || []).push(callback);
    },
  };

  if (webview) {
    webview.addEventListener("message", (e) => {
      const msg = e.data || {};
      (handlers[msg.type] || []).forEach((cb) => cb(msg));
    });
  }
})();
