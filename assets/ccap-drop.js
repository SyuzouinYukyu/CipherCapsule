/* CipherCapsule v1.1.6. GPL-2.0-or-later. Runs only in the trusted offline editor. */
(() => {
  'use strict';
  const bound = new WeakSet();
  function hasFiles(e) { return Array.from(e.dataTransfer?.types || []).includes('Files'); }
  function over(e) {
    if (!hasFiles(e)) return;
    e.preventDefault(); e.stopImmediatePropagation();
    e.dataTransfer.dropEffect = 'copy';
  }
  function drop(e) {
    if (!hasFiles(e)) return;
    e.preventDefault(); e.stopImmediatePropagation();
    const files = Array.from(e.dataTransfer.files || []);
    if (!files.some(f => /\.ccap$/i.test(f.name))) return;
    // Pass native File objects, never an arbitrary path from message JSON.
    window.chrome.webview.postMessageWithAdditionalObjects({type:'ccapDrop'}, files);
  }
  function bind(doc) {
    if (!doc || bound.has(doc)) return;
    bound.add(doc);
    doc.addEventListener('dragover', over, true);
    doc.addEventListener('drop', drop, true);
  }
  bind(document);
  bind(window.tinymce?.activeEditor?.getDoc());
  if (!window.ccDropEditorHook) {
    window.ccDropEditorHook = true;
    window.tinymce?.on('AddEditor', e => e.editor.on('init', () => bind(e.editor.getDoc())));
  }
})();
