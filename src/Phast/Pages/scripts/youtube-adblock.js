// Phast built-in YouTube ad blocker (original Phast code, same license as Phast).
// Injected at document start only into YouTube pages. It works in three layers:
//  1. removes the ad schedule (adPlacements, playerAds, adSlots...) from the player data before the player reads it;
//  2. if an ad still starts, mutes it, jumps to its end and presses "Skip";
//  3. hides ad boxes in the home feed, search results and sidebar.
(() => {
  "use strict";
  if (!/(^|\.)(youtube\.com|youtube-nocookie\.com)$/.test(location.hostname)) return;
  if (window.__phastYouTube) return;
  window.__phastYouTube = true;

  // ---- 1. Strip ads from player data -------------------------------------------------------
  const AD_KEYS = ["adPlacements", "adSlots", "playerAds", "adBreakHeartbeatParams"];

  const hasAds = (o) =>
    o && typeof o === "object" &&
    (AD_KEYS.some((k) => k in o) || (o.playerResponse && AD_KEYS.some((k) => k in o.playerResponse)));

  const prune = (o) => {
    if (!o || typeof o !== "object") return o;
    for (const k of AD_KEYS) {
      if (k in o) {
        try { delete o[k]; } catch { /* frozen object: ignore */ }
      }
    }
    if (o.playerResponse) prune(o.playerResponse);
    return o;
  };

  // Player data embedded in the HTML of a watch page.
  let initialPlayerResponse;
  try {
    Object.defineProperty(window, "ytInitialPlayerResponse", {
      configurable: true,
      get: () => initialPlayerResponse,
      set: (value) => { initialPlayerResponse = prune(value); },
    });
  } catch { /* already defined by the page */ }

  // Player data received later (navigating between videos) is parsed with JSON.parse or response.json().
  const originalParse = JSON.parse;
  JSON.parse = function (...args) {
    const result = originalParse.apply(this, args);
    return hasAds(result) ? prune(result) : result;
  };

  const originalJson = Response.prototype.json;
  Response.prototype.json = async function () {
    const data = await originalJson.call(this);
    return hasAds(data) ? prune(data) : data;
  };

  // ---- 2. Safety net: skip any ad that still plays ------------------------------------------
  const skipAd = () => {
    const player = document.querySelector(".html5-video-player");
    if (!player || !player.classList.contains("ad-showing")) return;
    const video = player.querySelector("video");
    if (video) {
      video.muted = true;
      if (Number.isFinite(video.duration) && video.duration > 0) video.currentTime = video.duration;
    }
    const skip = player.querySelector(".ytp-ad-skip-button, .ytp-ad-skip-button-modern, .ytp-skip-ad-button");
    if (skip) skip.click();
  };
  // A light timer is cheaper than observing every class change on YouTube's very busy DOM.
  setInterval(() => { if (!document.hidden) skipAd(); }, 300);

  // ---- 3. Hide ad boxes in feeds, search and sidebar -----------------------------------------
  const css = `
    ytd-ad-slot-renderer, ytd-in-feed-ad-layout-renderer, ytd-banner-promo-renderer,
    ytd-promoted-sparkles-web-renderer, ytd-promoted-sparkles-text-search-renderer,
    ytd-promoted-video-renderer, ytd-compact-promoted-video-renderer, ytd-display-ad-renderer,
    ytd-action-companion-ad-renderer, ytd-player-legacy-desktop-watch-ads-renderer,
    #masthead-ad, #player-ads, #panels > ytd-engagement-panel-section-list-renderer[target-id="engagement-panel-ads"],
    ytd-rich-item-renderer:has(> #content > ytd-ad-slot-renderer),
    ytd-rich-section-renderer:has(ytd-statement-banner-renderer),
    .ytp-ad-overlay-container, .ytp-ad-image-overlay, .ytp-featured-product,
    ytm-promoted-sparkles-web-renderer, ytm-companion-ad-renderer, ad-slot-renderer
    { display: none !important; }`;
  const addStyle = () => {
    const style = document.createElement("style");
    style.id = "phast-youtube-adblock";
    style.textContent = css;
    (document.head || document.documentElement).appendChild(style);
  };
  if (document.documentElement) addStyle();
  else document.addEventListener("DOMContentLoaded", addStyle, { once: true });
})();
