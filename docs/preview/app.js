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
 const term=$('search').value.toLowerCase(), filter=$('status').value;
 const visible=rows.filter(row=>row.name.toLowerCase().includes(term)&&(filter==='all'||(filter==='done'?row.state==='done':row.state!=='done')));
 $('file-rows').replaceChildren(...visible.map(row=>{
  const tr=document.createElement('tr');
  const name=document.createElement('td');name.textContent=row.name;name.title=row.name;
  const state=document.createElement('td'), badge=document.createElement('span');badge.className='badge '+row.state;badge.textContent=row.state==='done'?(row.operation==='pack'?'已压缩':'已解压'):row.state==='processing'?'处理中':'等待中';state.append(badge);
  const before=document.createElement('td');before.textContent=size(row.before);const after=document.createElement('td');after.textContent=row.after==null?'—':size(row.after);tr.append(name,state,before,after);return tr;
 }));
 $('empty').hidden=visible.length>0;$('empty-title').textContent=rows.length?'没有匹配的文件':'还没有文件';$('empty-copy').textContent=rows.length?'换个关键词，或选择全部状态。':'点“加入示例”，先感受一次完整流程。';$('empty-tutorial').hidden=rows.length>0;
 const completed=rows.filter(r=>r.state==='done'), total=rows.reduce((sum,r)=>sum+r.before,0), input=completed.reduce((sum,r)=>sum+r.before,0),output=completed.reduce((sum,r)=>sum+r.after,0);
 $('queue-count').textContent=rows.length;$('visible-count').textContent=`显示 ${visible.length} / ${rows.length}`;$('total-size').textContent=size(total);$('done-count').textContent=completed.length;$('saved-size').textContent=(input-output<0?'−':'')+size(Math.abs(input-output));$('input-size').textContent=size(input);$('output-size').textContent=completed.length?size(output):'—';setMeter('input-meter',input/Math.max(input,output,1));setMeter('output-meter',output/Math.max(input,output,1));
 $('run').disabled=running||!rows.some(r=>r.state==='waiting');$('add-example').disabled=running;
 document.querySelectorAll('[data-mode]').forEach(b=>b.disabled=running);document.querySelectorAll('[data-preset]').forEach(b=>b.disabled=running||mode==='unpack');
}
function addExamples(names){ if(running)return;const current=new Set(rows.map(r=>r.name));(names||['hello-world.exe','image-tools.dll','launcher.exe']).forEach((name,i)=>{if(!current.has(name)){rows.push({name,before:[8,4.8,2.5][i%3],after:null,state:'waiting'});current.add(name);}});$('summary').textContent='文件已加入';$('run-hint').textContent='当前为演示队列，点击开始查看处理反馈。';renderQueue(); }
$('add-example').onclick=()=>addExamples();$('search').oninput=renderQueue;$('status').onchange=renderQueue;
$('reset-queue').onclick=()=>{runGeneration++;running=false;rows=[];progress(0);$('summary').textContent='准备就绪';$('run-hint').textContent='加入示例后，就可以开始体验。';renderQueue();};
function setMode(value){if(running)return;mode=value;$('mode-options').querySelector('.pill').style.transform=`translateX(${value==='pack'?0:100}%)`;document.querySelectorAll('[data-mode]').forEach(b=>b.setAttribute('aria-pressed',b.dataset.mode===value));$('mode-hint').textContent=value==='pack'?'压缩后校验通过，再替换原文件。':'还原 UPX 压缩文件，体积会增大。';$('run').textContent=value==='pack'?'开始压缩':'开始解压';$('drop-title').textContent=value==='pack'?'投放文件':'投放待解压文件';renderQueue();}
document.querySelectorAll('[data-mode]').forEach(b=>b.onclick=()=>setMode(b.dataset.mode));
document.querySelectorAll('[data-preset]').forEach(b=>b.onclick=()=>{preset=Number(b.dataset.preset);$('preset-options').querySelector('.pill').style.transform=`translateX(${preset*100}%)`;document.querySelectorAll('[data-preset]').forEach(other=>other.setAttribute('aria-pressed',other===b));});
$('run').onclick=async()=>{if(running)return;const generation=++runGeneration,queue=rows.filter(r=>r.state==='waiting');running=true;renderQueue();let done=0;for(const row of queue){row.state='processing';$('summary').textContent='正在'+(mode==='pack'?'压缩':'解压')+' '+row.name;renderQueue();await pause(650);if(generation!==runGeneration)return;row.after=row.before*(mode==='pack'?[.4,.3,.26][preset]:2.5);row.state='done';row.operation=mode;progress(++done/queue.length*100);renderQueue();}running=false;$('summary').textContent=`处理完成 · ${queue.length} 个文件`;$('run-hint').textContent='可在队列中查看每个示例的结果。';renderQueue();};
let dragDepth=0;const drop=$('dropzone');
drop.ondragenter=e=>{e.preventDefault();dragDepth++;drop.classList.add('dragover');$('drop-title').textContent='松开鼠标，加入队列';};drop.ondragover=e=>{e.preventDefault();e.dataTransfer.dropEffect=running?'none':'copy';};
function resetDrag(){dragDepth=0;drop.classList.remove('dragover');$('drop-title').textContent=mode==='pack'?'投放文件':'投放待解压文件';}
drop.ondragleave=e=>{e.preventDefault();if(--dragDepth<=0)resetDrag();};drop.ondrop=e=>{e.preventDefault();resetDrag();const names=[...e.dataTransfer.files].map(f=>f.name).filter(n=>/\.(exe|dll)$/i.test(n));if(names.length)addExamples(names);else{$('summary').textContent='请拖入 EXE 或 DLL，也可点击“加入示例”。';}};
window.addEventListener('dragover',e=>e.preventDefault());window.addEventListener('drop',e=>e.preventDefault());
let step=0,lessonPreset=1,lessonReady=false,lessonGeneration=0,returnFocus=null;
const sizes=['3.2 MB','2.4 MB','2.1 MB'];
const titles=['先放进一个文件','选好强度，再压缩','结果，直接看得见'];
const bodies=['把 EXE、DLL 或文件夹拖进窗口，也可以用“添加文件”选择。先在左边试着加入一个示例。','日常使用选“均衡”就够了。你可以切换强度，观察示例的体积变化。真实文件开启“添加后自动开始”时，会立即处理。','队列会列出处理前后的体积和状态。失败的文件可以单独重试；需要整理结果时，可以导出 CSV。'];
function showLesson(){lessonGeneration++;lessonReady=step===2;$('step-label').textContent=`${step+1} / 3`;$('tutorial-title').textContent=titles[step];$('tutorial-body').textContent=bodies[step];$('tutorial-note').textContent=step===2?'正式处理时可开启“保留原文件备份”。带数字签名的文件会自动跳过；压缩校验通过后，发布前再运行程序确认功能。':'这里用的是演示数据，不会读取或修改你的文件，也不会改变正式队列的设置。';$('sample-status').textContent=step===0?'示例文件 · 8.0 MB':step===1?'已加入 · 等待压缩':'8.0 MB → '+sizes[lessonPreset];$('stage-hint').textContent=step===0?'点一下，把示例放进队列。':step===1?'当前强度：'+['快速','均衡','极致'][lessonPreset]:'已压缩 · 这就是队列里的完成状态。演示体积不代表实际压缩率。';$('tutorial-presets').hidden=step!==1;$('sample-file').classList.toggle('pending',step===0);$('practice-progress').style.width=step===2?'100%':'0%';$('practice-action').style.visibility=step===2?'hidden':'visible';$('practice-action').disabled=false;$('practice-action').textContent=step===0?'放入示例文件':'试一下压缩';$('tutorial-back').disabled=step===0;$('tutorial-next').textContent=step===2?'开始使用':'继续';$('tutorial-next').disabled=!lessonReady;document.querySelectorAll('[data-lesson]').forEach(b=>b.setAttribute('aria-pressed',Number(b.dataset.lesson)===lessonPreset));document.querySelectorAll('.step-dots i').forEach((dot,i)=>dot.classList.toggle('active',i<=step));$('tutorial-page').classList.remove('enter');void $('tutorial-page').offsetWidth;$('tutorial-page').classList.add('enter');}
function openTutorial(){returnFocus=document.activeElement;step=0;lessonPreset=1;showLesson();$('tutorial').showModal();$('practice-action').focus();}
function closeTutorial(){lessonGeneration++;$('tutorial').close();if(returnFocus&&returnFocus.isConnected)returnFocus.focus();}
['intro-tutorial','empty-tutorial','footer-tutorial'].forEach(id=>$(id).onclick=openTutorial);
$('tutorial-close').onclick=closeTutorial;$('tutorial').addEventListener('cancel',e=>{e.preventDefault();closeTutorial();});
$('tutorial-back').onclick=()=>{if(step>0){step--;showLesson();$('practice-action').focus();}};
$('tutorial-next').onclick=()=>{if(!lessonReady)return;if(step===2){closeTutorial();return;}step++;showLesson();(step===2?$('tutorial-next'):$('practice-action')).focus();};
document.querySelectorAll('[data-lesson]').forEach(b=>b.onclick=()=>{lessonPreset=Number(b.dataset.lesson);showLesson();});
$('practice-action').onclick=async()=>{const generation=lessonGeneration;$('practice-action').disabled=true;if(step===0){$('sample-file').classList.remove('pending');$('sample-status').textContent='已加入 · 等待压缩';$('stage-hint').textContent='文件已进入队列。下一步选择压缩强度。';$('practice-action').textContent='已加入队列';}else{$('sample-status').textContent='正在压缩示例…';$('practice-progress').style.width='100%';await pause(750);if(generation!==lessonGeneration||!$('tutorial').open)return;$('sample-status').textContent='8.0 MB → '+sizes[lessonPreset];$('stage-hint').textContent='压缩与校验已完成，继续查看结果。';$('practice-action').textContent='演练完成';}lessonReady=true;$('tutorial-next').disabled=false;$('tutorial-next').focus();};
$('try-tutorial').onclick=e=>{e.preventDefault();openTutorial();};$('try-mode').onclick=e=>{e.preventDefault();$('mode-options').scrollIntoView({behavior:reduced()?'instant':'smooth',block:'center'});setMode(mode==='pack'?'unpack':'pack');};
renderQueue();
