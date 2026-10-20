// phast://history — browsing history grouped by day (C# side: Bridge/HistoryBridge.cs).
(function () {
  const $ = (id) => document.getElementById(id);
  const esc = (s) => String(s ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
  const GLOBE = '<svg class="fav" viewBox="0 0 24 24"><circle cx="12" cy="12" r="9" fill="none" stroke="currentColor" stroke-width="1.6" opacity=".6"/></svg>';
  let items = [];
  const selected = new Set();
  let query = "";

  const dayLabel = (d) => {
    const today = new Date(); today.setHours(0, 0, 0, 0);
    const day = new Date(d); day.setHours(0, 0, 0, 0);
    const diff = Math.round((today - day) / 86400000);
    const long = d.toLocaleDateString("es-ES", { weekday: "long", day: "numeric", month: "long", year: "numeric" });
    return diff === 0 ? "Hoy - " + long : diff === 1 ? "Ayer - " + long : long[0].toUpperCase() + long.slice(1);
  };
  const host = (u) => { try { return new URL(u).hostname.replace(/^www\./, ""); } catch { return u; } };

  function render() {
    if (items.length === 0) {
      $("list").innerHTML = `<div class="card"><div class="empty">${query ? "No hay resultados" : "Tu historial está vacío"}</div></div>`;
      return;
    }
    const groups = new Map();
    for (const it of items) {
      const key = dayLabel(new Date(it.time));
      if (!groups.has(key)) groups.set(key, []);
      groups.get(key).push(it);
    }
    $("list").innerHTML = [...groups].map(([label, rows]) => `<div class="card"><h2>${esc(label)}</h2>${rows.map((it) => `
      <div class="row">
        <input type="checkbox" data-id="${it.id}" ${selected.has(it.id) ? "checked" : ""}>
        <span class="time">${new Date(it.time).toLocaleTimeString("es-ES", { hour: "2-digit", minute: "2-digit" })}</span>
        ${it.icon ? `<img class="fav" src="${it.icon}" alt="">` : GLOBE}
        <a href="${esc(it.url)}" title="${esc(it.url)}">${esc(it.title || it.url)}</a>
        <span class="host">${esc(host(it.url))}</span>
        <button class="icon-btn" data-del="${it.id}" title="Eliminar del historial">✕</button>
      </div>`).join("")}</div>`).join("");
  }

  function load(append) {
    const before = append && items.length ? items[items.length - 1].time : 0;
    phast.send("history.get", { query, before });
  }
  phast.on("history", (m) => {
    items = m.append ? items.concat(m.items) : m.items;
    $("more").hidden = m.items.length < 150;
    render();
  });
  phast.on("historyChanged", () => { selected.clear(); updateSelection(); load(false); });

  function updateSelection() {
    $("selection").classList.toggle("show", selected.size > 0);
    $("selCount").textContent = `${selected.size} seleccionado${selected.size === 1 ? "" : "s"}`;
  }
  $("list").addEventListener("change", (e) => {
    const id = Number(e.target.dataset.id);
    if (!id) return;
    e.target.checked ? selected.add(id) : selected.delete(id);
    updateSelection();
  });
  $("list").addEventListener("click", (e) => {
    const del = e.target.closest("[data-del]");
    if (del) phast.send("history.delete", { ids: [Number(del.dataset.del)] });
  });
  $("selDelete").addEventListener("click", () => phast.send("history.delete", { ids: [...selected] }));
  $("selCancel").addEventListener("click", () => { selected.clear(); updateSelection(); render(); });
  $("more").addEventListener("click", () => load(true));

  let timer;
  $("search").addEventListener("input", (e) => {
    clearTimeout(timer);
    timer = setTimeout(() => { query = e.target.value.trim(); load(false); }, 200);
  });

  // Clear browsing data
  $("clearBtn").addEventListener("click", () => $("clearDialog").showModal());
  $("cancelClear").addEventListener("click", () => $("clearDialog").close());
  $("doClear").addEventListener("click", () => {
    const kinds = [...$("clearDialog").querySelectorAll("input[type=checkbox]:checked")].map((c) => c.value);
    phast.send("browsingData.clear", { range: $("range").value, kinds });
    $("clearDialog").close();
  });
  let toastTimer;
  phast.on("toast", (m) => {
    $("toast").textContent = m.text;
    $("toast").classList.add("show");
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => $("toast").classList.remove("show"), 3000);
  });

  if (location.hash === "#clear") $("clearDialog").showModal();
  load(false);
})();
