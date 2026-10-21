/* CipherCapsule v1.1.0 editor integration. GPL-2.0-or-later. */
'use strict';
window.ccReady=false;
window.ccCsp=[];document.addEventListener('securitypolicyviolation',e=>window.ccCsp.push({directive:e.violatedDirective,uri:e.blockedURI}));
tinymce.init({
 selector:'#message',license_key:'gpl',base_url:'/tiny',suffix:'.min',language:'ja',language_url:'/ja.js',
 plugins:'advlist autolink lists link image charmap preview anchor searchreplace visualblocks code fullscreen insertdatetime table help wordcount',
 menubar:'edit view insert format table tools help',branding:true,promotion:false,statusbar:true,resize:false,height:'100%',
 toolbar_mode:'sliding',toolbar:
  'undo redo | blocks styles | fontfamily fontsize | bold italic underline strikethrough | forecolor backcolor | subscript superscript removeformat | '+
  'alignleft aligncenter alignright alignjustify | bullist numlist outdent indent blockquote | link unlink image table charmap hr | cccut cccopy ccpaste ccplain | searchreplace code help',
 font_family_formats:'游ゴシック=Yu Gothic UI;メイリオ=Meiryo;Arial=arial,helvetica,sans-serif;Times New Roman=times new roman,times,serif;Courier New=courier new,courier,monospace',
 font_size_formats:'8pt 9pt 10pt 11pt 12pt 14pt 16pt 18pt 24pt 36pt 48pt 72pt',
 style_formats:[{title:'段落',block:'p'},{title:'見出し1',block:'h1'},{title:'見出し2',block:'h2'},{title:'引用',block:'blockquote'},{title:'コード',block:'pre'}],
 valid_elements:'a[href|title|rel],p[style],div[style],span[style],br,hr,h1[style],h2[style],h3[style],h4[style],h5[style],h6[style],b,strong,i,em,u,s,small,sub,sup,pre[style],code,blockquote[style],ul[style],ol[style],li[style],table[style],colgroup,col[style],thead,tbody,tfoot,tr[style],td[colspan|rowspan|style],th[colspan|rowspan|style],caption,img[src|alt|width|height|style]',
 invalid_elements:'script,iframe,object,embed,form,input,button,svg,math,video,audio',
 allow_script_urls:false,allow_html_data_urls:false,allow_unsafe_link_target:false,link_default_protocol:'https',link_assume_external_targets:true,link_target_list:false,
 convert_urls:false,automatic_uploads:false,paste_data_images:true,images_file_types:'png,jpg,jpeg',image_title:false,image_description:true,image_dimensions:true,
 content_security_policy:"default-src 'none'; img-src data:; style-src 'unsafe-inline'; script-src 'none'; connect-src 'none'; form-action 'none'; base-uri 'none'",
 content_style:'body {font-family:Yu Gothic UI,Meiryo,sans-serif;font-size:12pt;} img{max-width:100%;height:auto} table{border-collapse:collapse}td,th{border:1px solid #999;padding:6px}',
 file_picker_types:'image',file_picker_callback:function(callback){window.ccImageCallback=callback;window.chrome.webview.postMessage({type:'pickImage'});},
 setup:function(ed){
   ed.on('init',()=>{window.ccReady=true;});
   [['cccut','cut','切り取り','cut'],['cccopy','copy','コピー','copy'],['ccpaste','paste','貼り付け','paste'],['ccplain','paste-text','テキストとして貼り付け','plain']].forEach(([name,icon,tooltip,action])=>ed.ui.registry.addButton(name,{icon,tooltip,onAction:()=>window.chrome.webview.postMessage({type:'clipboard',action})}));
   ed.on('BeforeSetContent',e=>{ /* C# sanitizes content before setContent. TinyMCE also applies its schema. */ });
   ed.on('paste',function(e){
      const text=e.clipboardData?.getData('text/plain')||'';
      if(!ed.selection.isCollapsed()){
        try{const u=new URL(text);if((u.protocol==='http:'||u.protocol==='https:')&&!u.username&&!u.password){e.preventDefault();e.stopImmediatePropagation();ed.undoManager.transact(()=>ed.execCommand('mceInsertLink',false,{href:u.href}));}}
        catch{}
      }
   },true);
   ed.on('click',e=>{if(e.target.closest('a'))e.preventDefault();});
   ed.on('BeforeGetContent',()=>{ed.dom.select('a').forEach(a=>{try{const u=new URL(a.getAttribute('href'));if(!['http:','https:'].includes(u.protocol))ed.dom.setAttrib(a,'href',null);}catch{ed.dom.setAttrib(a,'href',null);}});});
 }
}).catch(e=>{window.ccError=String(e);});
