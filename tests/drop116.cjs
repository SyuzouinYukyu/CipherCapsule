'use strict';
const fs=require('node:fs'),path=require('node:path'),vm=require('node:vm');
const source=path.resolve(process.argv[2]),out=path.resolve(process.argv[3]);
const script=fs.readFileSync(path.join(source,'assets','ccap-drop.js'),'utf8');
class Doc {
  constructor(){this.listeners={};}
  addEventListener(name,handler,capture){(this.listeners[name]??=[]).push({handler,capture});}
  fire(name,event){for(const entry of this.listeners[name]||[])entry.handler(event);}
}
function setup(){
  const document=new Doc(),inner=new Doc(),sent=[],hooks={};
  const window={chrome:{webview:{postMessageWithAdditionalObjects:(message,files)=>sent.push({message,files})}},tinymce:{activeEditor:{getDoc:()=>inner},on:(name,handler)=>hooks[name]=handler}};
  vm.runInNewContext(script,{window,document});return{document,inner,sent,hooks};
}
function event(names=[],types=['Files']){
  return{dataTransfer:{types,files:names.map(name=>({name})),dropEffect:'none'},prevented:false,stopped:false,preventDefault(){this.prevented=true;},stopImmediatePropagation(){this.stopped=true;}};
}
function check(ok){if(!ok)throw new Error('assertion failed');}
const tests=[];
function test(name,body){try{body();tests.push({name,passed:true});console.log('PASS '+name);}catch(error){tests.push({name,passed:false,error:String(error)});console.log('FAIL '+name+' '+error);}}
test('top document and TinyMCE iframe receive file handlers',()=>{const s=setup();check(s.document.listeners.drop.length===1&&s.inner.listeners.drop.length===1);});
test('file handlers use capture before editor insertion',()=>{const s=setup();check(s.document.listeners.drop[0].capture&&s.inner.listeners.dragover[0].capture);});
test('top-level CCAP sends exact native-file objects without path JSON',()=>{const s=setup(),e=event(['日本語 空白=🔐.ccap']);s.document.fire('drop',e);check(e.prevented&&e.stopped&&s.sent.length===1&&s.sent[0].files[0]===e.dataTransfer.files[0]&&Object.keys(s.sent[0].message).join()=='type'&&s.sent[0].message.type==='ccapDrop');});
test('iframe CCAP uses the top-level WebView message bridge',()=>{const s=setup();s.inner.fire('drop',event(['body.ccap']));check(s.sent.length===1&&s.sent[0].message.type==='ccapDrop');});
test('uppercase CCAP is recognized',()=>{const s=setup();s.document.fire('drop',event(['BODY.CCAP']));check(s.sent.length===1);});
test('mixed files all reach the existing one-file validator',()=>{const s=setup();s.inner.fire('drop',event(['one.txt','two.ccap']));check(s.sent.length===1&&s.sent[0].files.length===2);});
test('ordinary external files cannot be inserted into HTML',()=>{const s=setup(),e=event(['photo.png']);s.inner.fire('drop',e);check(e.prevented&&e.stopped&&s.sent.length===0);});
test('ordinary editor text drag is preserved',()=>{const s=setup(),e=event([],['text/plain']);s.inner.fire('drop',e);check(!e.prevented&&!e.stopped&&s.sent.length===0);});
test('file dragover permits a copy drop',()=>{const s=setup(),e=event();s.inner.fire('dragover',e);check(e.prevented&&e.stopped&&e.dataTransfer.dropEffect==='copy');});
test('text dragover keeps normal editor handling',()=>{const s=setup(),e=event([],['text/plain']);s.document.fire('dragover',e);check(!e.prevented&&!e.stopped);});
test('new editor iframe is bound after initialization',()=>{const s=setup(),doc=new Doc();let initialize;const editor={getDoc:()=>doc,on:(name,handler)=>{check(name==='init');initialize=handler;}};s.hooks.AddEditor({editor});initialize();doc.fire('drop',event(['new.ccap']));check(s.sent.length===1);});
test('repeated iframe initialization does not double-deliver',()=>{const s=setup();let initialize;const editor={getDoc:()=>s.inner,on:(_,handler)=>initialize=handler};s.hooks.AddEditor({editor});initialize();s.inner.fire('drop',event(['one.ccap']));check(s.sent.length===1);});
test('empty file drop cannot send a path request',()=>{const s=setup();s.document.fire('drop',event([]));check(s.sent.length===0);});
test('CCAP text inside a different extension is not treated as CCAP',()=>{const s=setup();s.inner.fire('drop',event(['one.ccap.txt']));check(s.sent.length===0);});
const failed=tests.filter(t=>!t.passed).length;
fs.mkdirSync(path.dirname(out),{recursive:true});fs.writeFileSync(out,JSON.stringify({version:'1.1.6',passed:tests.length-failed,failed,headless:true,desktop_input:false,real_browser_used:false,tests},null,2));
console.log(`RESULT ${tests.length-failed}/${failed}`);process.exitCode=failed?1:0;
