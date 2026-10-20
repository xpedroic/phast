// Phast element hiding helper (original Phast code). Injected into every page while "hide ad gaps" is on.
// It tells the browser which class names and ids the page uses; the browser answers with CSS that hides
// only the ad containers among them. New elements added later are reported in small batches.
(() => {
  "use strict";
  if (window.top !== window || window.__phastCosmetic || !window.chrome || !window.chrome.webview) return;
  if (!/^https?:$/.test(location.protocol) || location.hostname === "phast.internal") return;
  window.__phastCosmetic = true;

  const seen = new Set();
  let pending = [];
  let timer = 0;

  const collect = (root) => {
    const add = (el) => {
      if (el.id && !seen.has("#" + el.id)) { seen.add("#" + el.id); pending.push("#" + el.id); }
      const list = el.classList;
      if (list) for (let i = 0; i < list.length; i++) {
        const token = "." + list[i];
        if (!seen.has(token)) { seen.add(token); pending.push(token); }
      }
    };
    if (root.nodeType !== 1) return;
    add(root);
    for (const el of root.querySelectorAll("[id],[class]")) add(el);
  };

  // The first report is always sent (even if empty): it also brings the site's own hiding rules.
  const flush = (force) => {
    timer = 0;
    if (pending.length === 0 && !force) return;
    const batch = pending;
    pending = [];
    window.chrome.webview.postMessage({ phastCosmetic: batch.slice(0, 8000) });
  };

  const start = () => {
    collect(document.documentElement);
    flush(true);
    // Busy pages add nodes constantly: only remember them here and scan them in batches, when the page is idle.
    let added = [];
    const scan = () => {
      timer = 0;
      const nodes = added;
      added = [];
      for (const n of nodes) if (n.isConnected) collect(n);
      flush();
    };
    new MutationObserver((mutations) => {
      for (const m of mutations) for (const n of m.addedNodes) if (n.nodeType === 1) added.push(n);
      if (added.length && !timer)
        timer = window.requestIdleCallback ? requestIdleCallback(scan, { timeout: 500 }) : setTimeout(scan, 250);
    }).observe(document.documentElement, { childList: true, subtree: true });
  };

  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", start, { once: true });
  else start();
})();
