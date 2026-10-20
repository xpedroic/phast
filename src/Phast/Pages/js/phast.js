// Tiny bridge between Phast's internal pages and the browser (C# side: Bridge/PageBridge.cs).
// Usage: phast.send("navigate", { text: "github.com" });  phast.on("someEvent", data => ...);
(function () {
  const handlers = {};
  // Only talk to the browser when this page is the top-level document (never when embedded in a frame).
  const webview = window.top === window && window.chrome && window.chrome.webview;

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
