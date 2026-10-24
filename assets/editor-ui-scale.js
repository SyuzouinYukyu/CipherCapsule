/* CipherCapsule v1.1.8. GPL-2.0-or-later. Outer TinyMCE UI only; never touches the editor iframe. */
(() => {
  'use strict';
  let latest = -1, current = null, style = null, observer = null, frame = 0;
  const decimalPx = /(-?(?:\d*\.)?\d+)px\b/g;
  const protectedValues = new Set(['content', 'background-image', 'font-family']);
  function scaledRules(rules) {
    let result = '';
    for (const rule of rules) {
      if (rule.type === CSSRule.STYLE_RULE && rule.selectorText.includes('.tox')) {
        const declarations = [];
        for (const name of rule.style) {
          const value = rule.style.getPropertyValue(name);
          if (!protectedValues.has(name) && decimalPx.test(value)) {
            decimalPx.lastIndex = 0;
            declarations.push(`${name}:${value.replace(decimalPx, 'calc($1px * var(--cc-ui-scale))')}${rule.style.getPropertyPriority(name) ? ' !important' : ''};`);
          }
          decimalPx.lastIndex = 0;
        }
        if (declarations.length) result += `${rule.selectorText}{${declarations.join('')}}\n`;
      } else if (rule.cssRules && (rule.type === CSSRule.MEDIA_RULE || rule.type === CSSRule.SUPPORTS_RULE)) {
        result += `${rule.cssText.slice(0, rule.cssText.indexOf('{'))}{${scaledRules(rule.cssRules)}}\n`;
      }
    }
    return result;
  }
  function icons() {
    document.querySelectorAll('.tox svg[width][height]').forEach(svg => {
      const w = Number(svg.getAttribute('width')), h = Number(svg.getAttribute('height'));
      if (Number.isFinite(w) && Number.isFinite(h) && w > 0 && h > 0 && w <= 256 && h <= 256) {
        svg.style.setProperty('--cc-icon-width', `${w}px`);
        svg.style.setProperty('--cc-icon-height', `${h}px`);
      }
    });
  }
  function install() {
    if (style?.isConnected) return;
    const skin = [...document.styleSheets].find(s => s.href && new URL(s.href).pathname.endsWith('/skins/ui/oxide/skin.min.css'));
    if (!skin) throw new Error('TinyMCE UI skin has not loaded');
    style = document.createElement('style'); style.id = 'cc-ui-dimensions';
    // Copy only dimension-bearing UI declarations into an application-owned sheet.
    // The original vendor stylesheet remains unchanged, and values always derive from its 12pt baseline.
    style.textContent = scaledRules(skin.cssRules) + `
      .tox .tox-tbtn,.tox .tox-mbtn,.tox .tox-listboxfield .tox-listbox--select,
      .tox .tox-collection__item-label,.tox .tox-statusbar{font-size:var(--cc-ui-font)}
      .tox .tox-mbtn{width:auto;max-width:none;min-width:max-content;white-space:nowrap;flex-shrink:0;height:auto;min-height:calc(28px * var(--cc-ui-scale))}
      .tox .tox-mbtn__select-label{white-space:nowrap}
      /* Silver freezes toolbar widths inline during setup; override only these UI widths. */
      .tox .tox-tbtn:not(.tox-tbtn--select){width:calc(34px * var(--cc-ui-scale)) !important}
      .tox .tox-tbtn--select{width:auto !important;min-width:calc(130px * var(--cc-ui-scale))}
      .tox .tox-split-button__chevron,.tox .tox-tbtn--return{width:calc(16px * var(--cc-ui-scale)) !important}
      .tox .tox-tbtn--md{width:calc(51px * var(--cc-ui-scale)) !important}
      .tox .tox-tbtn--lg{width:calc(68px * var(--cc-ui-scale)) !important}
      .tox svg[width][height]{width:calc(var(--cc-icon-width) * var(--cc-ui-scale));height:calc(var(--cc-icon-height) * var(--cc-ui-scale));flex-shrink:0}
      .tox .tox-statusbar{height:auto;min-height:calc(25px * var(--cc-ui-scale));flex-shrink:0}
      .tox .tox-statusbar__text-container{height:auto;min-height:1.4em;line-height:1.4}
      .tox .tox-statusbar__path,.tox .tox-statusbar__wordcount,.tox .tox-statusbar__branding{height:auto;line-height:1.4}
      .tox .tox-editor-container,.tox .tox-edit-area{min-height:0}
    `;
    document.head.appendChild(style); icons();
    observer?.disconnect(); observer = new MutationObserver(icons);
    observer.observe(document.body, {childList:true, subtree:true});
  }
  function reflow() {
    cancelAnimationFrame(frame);
    frame = requestAnimationFrame(() => {
      if (!window.tinymce?.activeEditor?.removed) window.dispatchEvent(new Event('resize'));
    });
  }
  window.ccUiScale = Object.freeze({
    apply(size, revision) {
      if (typeof size !== 'number' || !Number.isFinite(size) || size < 9 || size > 17 ||
          !Number.isSafeInteger(revision) || revision < 0 || revision < latest) return false;
      latest = revision; install();
      const scale = size / 12;
      document.documentElement.style.setProperty('--cc-ui-scale', String(scale));
      document.documentElement.style.setProperty('--cc-ui-font', `${size}pt`);
      if (current !== size) { current = size; reflow(); }
      return true;
    },
    dispose() { observer?.disconnect(); cancelAnimationFrame(frame); style?.remove(); style=null; current=null; }
  });
})();
