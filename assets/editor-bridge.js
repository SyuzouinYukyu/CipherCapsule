/* CipherCapsule v1.1.8: bounded, typed editor replies; no HTML or exception text in diagnostics. */
(() => {
  'use strict';let latest=0;
  window.ccEditorBridge = Object.freeze({
    run(command, revision, expires, html) {
      try {
        if (!Number.isSafeInteger(revision) || revision < latest) return {ok:false,kind:'Stale'};
        if (!Number.isFinite(expires) || Date.now()>expires) return {ok:false,kind:'Expired'};
        latest=revision;
        const editor=window.tinymce?.activeEditor;
        if (!window.ccReady || !editor || editor.removed || !editor.initialized) return {ok:false,kind:'NotReady'};
        if(command==='read') {
          const value=editor.getContent();
          return typeof value==='string'?{ok:true,value}:{ok:false,kind:'InvalidResult'};
        }
        if(command==='write' && typeof html==='string') {
          // A no-op roundtrip must not reset selection, caret or undo history.
          if(editor.getContent()!==html) editor.setContent(html);
          return {ok:true,value:''};
        }
        return {ok:false,kind:'InvalidCommand'};
      } catch(error) { return {ok:false,kind:'ScriptFailure'}; }
    }
  });
})();
