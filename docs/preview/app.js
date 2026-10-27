'use strict';
const $ = id => document.getElementById(id);
const reduced = () => $('reduce-motion').checked;
$('reduce-motion').checked = matchMedia('(prefers-reduced-motion: reduce)').matches;
$('reduce-motion').onchange = () => document.body.classList.toggle('reduce-motion', reduced());
$('reduce-motion').onchange();
let rows=[], mode='pack', preset=1, running=false, runGeneration=0;
const size = n => n ? n.toFixed(1)+' MB' : '0 B';
const pause = ms => new Promise(resolve=>setTimeout(resolve,reduced()?0:ms));
function setMeter(id,ratio){ $(id).replaceChildren(...Array.from({length:24},(_,i)=>{const tick=document.createElement('i');tick.className=i<Math.ceil(ratio*24)?'on':'';return tick;})); }
function progress(value){$('progress-fill').style.width=value+'%';$('progress-fill').parentElement.setAttribute('aria-valuenow',value);}
function renderQueue(){
 $('mode-hint').textContent=mode==='pack'?['优先处理速度。','兼顾处理速度和文件大小。','尝试更充分的压缩，耗时较长。'][preset]:'还原 UPX 压缩过的文件，体积会增大。';
 const term=$('search').value.toLowerCase(), filter=$('status').value;
 const visible=rows.filter(row=>row.name.toLowerCase().includes(term)&&(filter==='all'||(filter==='done'?row.state==='done':row.state!=='done')));
 $('file-rows').replaceChildren(...visible.map(row=>{
  const tr=document.createElement('tr');
  const name=document.createElement('td');name.textContent=row.name;name.title=row.name;
  const state=document.createElement('td'), badge=document.createElement('span');badge.className='badge '+row.state;badge.textContent=row.state==='done'?(row.operation==='pack'?'已压缩':'已解压'):row.state==='processing'?'处理中':'等待中';state.append(badge);
  const before=document.createElement('td');before.textContent=size(row.before);const after=document.createElement('td');after.textContent=row.after==null?'—':size(row.after);tr.append(name,state,before,after);return tr;
 }));
 $('empty').hidden=visible.length>0;$('empty-title').textContent=rows.length?'没有匹配的文件':'还没有文件';$('empty-copy').textContent=rows.length?'换个关键词，或选择全部状态。':'点“添加示例”加入文件。';$('empty-tutorial').hidden=rows.length>0;
 const completed=rows.filter(r=>r.state==='done'), total=rows.reduce((sum,r)=>sum+r.before,0), input=completed.reduce((sum,r)=>sum+r.before,0),output=completed.reduce((sum,r)=>sum+r.after,0);
 $('queue-count').textContent=rows.length;$('visible-count').textContent=`显示 ${visible.length} / ${rows.length}`;$('total-size').textContent=size(total);$('done-count').textContent=completed.length;$('saved-size').textContent=(input-output<0?'−':'')+size(Math.abs(input-output));$('input-size').textContent=size(input);$('output-size').textContent=completed.length?size(output):'—';setMeter('input-meter',input/Math.max(input,output,1));setMeter('output-meter',output/Math.max(input,output,1));
 $('run').disabled=running||!rows.some(r=>r.state==='waiting');$('add-example').disabled=running;
 document.querySelectorAll('[data-mode]').forEach(b=>b.disabled=running);document.querySelectorAll('[data-preset]').forEach(b=>b.disabled=running||mode==='unpack');
}
function addExamples(names){ if(running)return;const current=new Set(rows.map(r=>r.name));(names||['hello-world.exe','image-tools.dll','launcher.exe']).forEach((name,i)=>{if(!current.has(name)){rows.push({name,before:[8,4.8,2.5][i%3],after:null,state:'waiting'});current.add(name);}});$('summary').textContent='文件已加入';$('run-hint').textContent='点击下方按钮开始处理。';renderQueue(); }
$('add-example').onclick=()=>addExamples();$('search').oninput=renderQueue;$('status').onchange=renderQueue;
$('reset-queue').onclick=()=>{runGeneration++;running=false;rows=[];progress(0);$('summary').textContent='准备就绪';$('run-hint').textContent='先添加示例。';renderQueue();};
function setMode(value){if(running)return;mode=value;$('mode-options').querySelector('.pill').style.transform=`translateX(${value==='pack'?0:100}%)`;document.querySelectorAll('[data-mode]').forEach(b=>b.setAttribute('aria-pressed',b.dataset.mode===value));$('run').textContent=value==='pack'?'开始压缩':'开始解压';$('drop-title').textContent=value==='pack'?'拖入文件':'拖入待解压文件';renderQueue();}
document.querySelectorAll('[data-mode]').forEach(b=>b.onclick=()=>setMode(b.dataset.mode));
document.querySelectorAll('[data-preset]').forEach(b=>b.onclick=()=>{preset=Number(b.dataset.preset);$('preset-options').querySelector('.pill').style.transform=`translateX(${preset*100}%)`;document.querySelectorAll('[data-preset]').forEach(other=>other.setAttribute('aria-pressed',other===b));renderQueue();});
$('run').onclick=async()=>{if(running)return;const generation=++runGeneration,queue=rows.filter(r=>r.state==='waiting');running=true;renderQueue();let done=0;for(const row of queue){row.state='processing';$('summary').textContent='正在'+(mode==='pack'?'压缩':'解压')+' '+row.name;renderQueue();await pause(650);if(generation!==runGeneration)return;row.after=row.before*(mode==='pack'?[.4,.3,.26][preset]:2.5);row.state='done';row.operation=mode;progress(++done/queue.length*100);renderQueue();}running=false;$('summary').textContent=`处理完成 · ${queue.length} 个文件`;$('run-hint').textContent='处理结果已显示在队列中。';renderQueue();};
let dragDepth=0;const drop=$('dropzone');
drop.ondragenter=e=>{e.preventDefault();dragDepth++;drop.classList.add('dragover');$('drop-title').textContent='松开鼠标，加入队列';};drop.ondragover=e=>{e.preventDefault();e.dataTransfer.dropEffect=running?'none':'copy';};
function resetDrag(){dragDepth=0;drop.classList.remove('dragover');$('drop-title').textContent=mode==='pack'?'拖入文件':'拖入待解压文件';}
drop.ondragleave=e=>{e.preventDefault();if(--dragDepth<=0)resetDrag();};drop.ondrop=e=>{e.preventDefault();resetDrag();const names=[...e.dataTransfer.files].map(f=>f.name).filter(n=>/\.(exe|dll)$/i.test(n));if(names.length)addExamples(names);else{$('summary').textContent='请拖入 EXE 或 DLL，也可点击“添加示例”。';}};
window.addEventListener('dragover',e=>e.preventDefault());window.addEventListener('drop',e=>e.preventDefault());
let step=0,lessonGeneration=0,returnFocus=null;
const titles=['添加文件','选择压缩强度','查看处理结果'];
const bodies=['把 EXE、DLL 或文件夹拖进窗口，也可以点“添加文件”。','选好强度后，点“开始压缩”。平时用“均衡”就行，想快一点可以选“快速”。','这里能看到文件状态和前后大小。处理失败就点“重试未完成”，要保存结果就导出 CSV。'];
function showLesson(){
 const generation=++lessonGeneration;
 $('step-label').textContent=`${step+1} / 3`;
 $('tutorial-title').textContent=titles[step];$('tutorial-body').textContent=bodies[step];
 $('tutorial-note').textContent=['要一次加入多个文件，可以直接拖入文件夹。','“添加后自动开始”打开时，加入文件就会开始处理。','想留下原文件，处理前勾选“保留原文件备份”。'][step];
 $('sample-status').textContent=step===0?'拖入文件 · 8.0 MB':step===1?'正在压缩…':'已压缩 · 8.0 MB → 2.4 MB';
 $('tutorial-presets').hidden=step!==1;$('sample-file').classList.toggle('pending',step===0);
 $('practice-progress').parentElement.style.visibility=step===0?'hidden':'visible';
 $('practice-progress').style.width=step===2?'100%':'0%';
 $('tutorial-back').disabled=step===0;$('tutorial-next').textContent=step===2?'开始使用':'下一步';$('tutorial-next').disabled=false;
 document.querySelectorAll('.step-dots i').forEach((dot,i)=>dot.classList.toggle('active',i<=step));
 $('tutorial-page').classList.remove('enter');void $('tutorial-page').offsetWidth;$('tutorial-page').classList.add('enter');
 playPreview(generation,step);
}
async function playPreview(generation,page){
 if(page===2)return;
 await pause(120);
 if(generation!==lessonGeneration||!$('tutorial').open)return;
 if(page===0)$('sample-file').classList.remove('pending');else $('practice-progress').style.width='100%';
 await pause(page===0?350:700);
 if(generation!==lessonGeneration||!$('tutorial').open)return;
 $('sample-status').textContent=page===0?'已加入队列 · 8.0 MB':'已压缩 · 8.0 MB → 2.4 MB';
}
function openTutorial(){returnFocus=document.activeElement;step=0;$('tutorial').showModal();showLesson();$('tutorial-next').focus();}
function closeTutorial(){lessonGeneration++;$('tutorial').close();if(returnFocus&&returnFocus.isConnected)returnFocus.focus();}
['empty-tutorial','footer-tutorial'].forEach(id=>$(id).onclick=openTutorial);
$('tutorial-close').onclick=closeTutorial;$('tutorial').addEventListener('cancel',e=>{e.preventDefault();closeTutorial();});
$('tutorial-back').onclick=()=>{if(step>0){step--;showLesson();$('tutorial-next').focus();}};
$('tutorial-next').onclick=()=>{if(step===2){closeTutorial();return;}step++;showLesson();$('tutorial-next').focus();};
renderQueue();
