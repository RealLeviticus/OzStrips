namespace MaxRumsey.OzStripsPlugin.GUI;

/// <summary>
/// Contains the single-page HTML application for the web viewer.
/// </summary>
internal static class WebViewerHtml
{
    /// <summary>
    /// Gets the full HTML page served to web clients.
    /// </summary>
    public const string Html = """
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width,initial-scale=1,maximum-scale=1,user-scalable=no" />
<title>OzStrips Web</title>
<style>
:root{
  --main-gray:#a0aaaa;
  --menu-text:#000060;
  --app-gray:#808080;
  --bay-gray:#404040;
  --btn-gray:#8c9696;
  --strip-scale:1;
}
*,*::before,*::after{box-sizing:border-box}
html,body{margin:0;height:100%;overflow:hidden;background:var(--app-gray);font-family:"Terminus (TTF)",Consolas,"Lucida Console",monospace;color:#000;-webkit-user-select:none;user-select:none;-webkit-tap-highlight-color:transparent;touch-action:manipulation}
button,input,select{font:inherit}
#app{height:100vh;display:flex;flex-direction:column}
.waiting{display:flex;align-items:center;justify-content:center;height:100%;font-size:14px;color:#111;background:#8b8b8b}

.top-menu{height:25px;min-height:25px;display:flex;align-items:center;gap:8px;padding:0 4px;background:var(--main-gray);border-bottom:1px solid #6e7676;color:var(--menu-text);font-size:16px;font-weight:700;white-space:nowrap;overflow-x:auto}
.top-menu .label{opacity:.95;padding:0 6px}
.top-menu .menu-item{cursor:pointer}
.top-menu .menu-item:active{filter:brightness(.92)}
.top-menu .menu-item.open{background:#c4cccc;border:1px solid #6e7676;border-bottom:none;border-radius:2px 2px 0 0}
.top-menu .pending{padding:0 6px;border-radius:2px}
.top-menu .pending.active{background:lightblue}
.top-menu .autofill{padding:0 6px;border-radius:2px}
.top-menu .autofill.on{background:lightgreen}
.top-menu .autofill.off{background:red;color:var(--menu-text)}

.menu-layer{position:fixed;inset:25px 0 0 0;z-index:90}
.menu-backdrop{position:absolute;inset:0}
.menu-panel{position:absolute;top:0;min-width:220px;background:#d5dede;border:1px solid #606868;box-shadow:2px 2px 0 #2f3434;padding:2px}
.menu-row{height:28px;border:1px solid transparent;background:#d5dede;color:#000060;font-size:13px;font-weight:700;text-align:left;padding:0 8px;cursor:pointer;display:flex;align-items:center;gap:6px;width:100%}
.menu-row:hover{background:#bfcaca}
.menu-row:disabled{opacity:.45;cursor:default}
.menu-sep{height:1px;background:#7f8787;margin:2px 0}
.menu-check{display:inline-block;width:14px}

.main{flex:1;overflow:auto;padding:0;background:var(--main-gray);-webkit-overflow-scrolling:touch;touch-action:pan-x pan-y}
.columns{display:flex;gap:0;min-height:100%}
.column{min-width:min(481px,100vw);flex:1 0 min(481px,100vw);display:flex;flex-direction:column;gap:0;padding:2px}
.bay{background:var(--bay-gray);display:flex;flex-direction:column;min-height:90px;flex:1 1 0}
.bay-top{height:36px;min-height:36px;background:#000;display:flex;align-items:center;padding:0 6px;gap:6px}
.bay-name{width:150px;min-width:150px;color:#fff;font-family:"Segoe UI",sans-serif;font-weight:900;font-size:16px;line-height:1;text-align:left}
.bay-spacer{flex:1 1 auto}
.bay-btn{height:24px;padding:0 6px;border:1px solid #5b6363;background:#8c9696;color:#000060;font-family:"Terminus (TTF)",Consolas,"Lucida Console",monospace;font-size:13px;font-weight:700;cursor:pointer;white-space:nowrap}
.bay-btn.queue{width:116px}
.bay-btn.div{width:145px}
.bay-btn:active{filter:brightness(.94)}
.bay-body{flex:1;overflow-y:auto;padding:0;background:#404040;-webkit-overflow-scrolling:touch;touch-action:none}
.bay-empty{padding:10px 4px;color:#bbb;font-size:12px;text-align:center}

.strip-shell{position:relative;width:calc(424px * var(--strip-scale));height:calc(64px * var(--strip-scale));margin:0;cursor:default}
.strip-shell.dep{background:#c1e6f2}
.strip-shell.arr{background:#ffffa0}
.strip-shell.loc{background:#e6aedd}
.strip-shell.unk{background:#404040}
.strip-shell.cock1{margin-left:calc(30px * var(--strip-scale))}
.strip-canvas{position:absolute;left:0;top:0;width:424px;height:64px;transform:scale(var(--strip-scale));transform-origin:top left}
.strip-grid{position:absolute;left:2px;top:2px;width:420px;height:60px;border:1px solid #000;display:grid;grid-template-columns:50px 25px 25px 25px 25px 25px 75px 30px 50px 60px 30px;grid-template-rows:20px 20px 20px;font-family:"Segoe UI",sans-serif;font-size:12px;font-weight:700}
.cell{border:1px solid #000;display:flex;align-items:center;justify-content:center;padding:0 2px;overflow:hidden;white-space:nowrap;text-overflow:ellipsis;line-height:1}
.cell.tap{cursor:pointer}

.c-stand{grid-column:1;grid-row:1}
.c-eobt{grid-column:2/4;grid-row:1}
.c-type{grid-column:4/6;grid-row:1}
.c-wtc{grid-column:6;grid-row:1}
.c-acid{grid-column:7;grid-row:1/3;font-size:14px}
.c-rwy{grid-column:8;grid-row:1}
.c-clx{grid-column:9;grid-row:1/3}
.c-sid{grid-column:10;grid-row:1;background:limegreen}
.c-rfl{grid-column:11;grid-row:1;color:gray}
.c-ades{grid-column:1;grid-row:2}
.c-route{grid-column:2;grid-row:2}
.c-frul{grid-column:3;grid-row:2}
.c-pdc{grid-column:4;grid-row:2}
.c-ssr{grid-column:5/7;grid-row:2}
.c-ready{grid-column:8;grid-row:2}
.c-fwpt{grid-column:10;grid-row:2}
.c-cfl{grid-column:11;grid-row:2}
.c-glop{grid-column:1/7;grid-row:3}
.c-remark{grid-column:7/9;grid-row:3}
.c-freq{grid-column:9/11;grid-row:3}
.c-tot{grid-column:11;grid-row:3;font-size:10px}

.strip-shell.cock1 .cock-zone{background:cyan}
.strip-shell.crossing .cross-zone{background:red}
.cell.bg-lightgray{background:lightgray}
.cell.bg-yellow{background:yellow}
.cell.bg-orange{background:orange}
.cell.bg-lime{background:lime}
.cell.bg-limegreen{background:limegreen}
.cell.bg-pink{background:pink}
.cell.bg-silver{background:silver}
.cell.bg-world{background:yellow}
.cell.pdc-blink{animation:pdcBlink 2s steps(1,end) infinite}
.cell.sid-transition{box-shadow:inset 0 0 0 2px yellow}
.cell.tot-running{color:green}
@keyframes pdcBlink{0%,49%{background:#fff}50%,100%{background:yellow}}

.bar-shell{position:relative;width:calc(424px * var(--strip-scale));height:calc(30px * var(--strip-scale));margin:0}
.bar-item{position:absolute;left:0;top:0;width:424px;height:30px;transform:scale(var(--strip-scale));transform-origin:top left;border:1px solid #000;background:gray;color:#000;display:flex;align-items:center;justify-content:center;font-family:"Segoe UI",sans-serif;font-size:18px;font-weight:700;line-height:1}
.bar-item.style1{background:lightgray}
.bar-item.style2{background:orange}
.bar-item.style3{background:red;color:#fff}

.control-bar{height:45px;min-height:45px;display:flex;align-items:center;gap:4px;padding:3px;background:var(--main-gray);border-top:1px solid #6e7676;overflow-x:auto}
.conn-stat{position:relative;flex:0 0 96px;height:37px;display:flex;align-items:center;justify-content:center;color:#000;font-size:16px;font-weight:700;--core:#ff4500;--outer:#ff4500}
.conn-stat.connected{--core:#008000}
.conn-stat.multi{--outer:#0000ff}
.conn-stat::before,.conn-stat::after{content:"";position:absolute;inset:0}
.conn-stat::before{background:var(--outer)}
.conn-stat::after{inset:5px;background:var(--core)}
.conn-stat span{position:relative;z-index:1}
.time-box{flex:0 0 137px;height:37px;background:#ffffe1;border:1px solid #555;color:#000060;display:flex;align-items:center;justify-content:center;font-size:27px;font-weight:700}
.ad-wrap{flex:0 0 96px;height:37px;padding:1px;background:darkgray}
.ad-box{height:100%;background:silver;color:#000060;display:flex;align-items:center;justify-content:center;font-size:15px;font-weight:700}
.atis-wrap{flex:0 0 42px;height:37px;padding:1px;background:darkgray}
.atis-box{height:100%;background:silver;color:#000060;display:flex;align-items:center;justify-content:center;font-size:22px;font-weight:700}
.ctl-btn{height:37px;padding:0 10px;border:1px solid #6b7474;background:var(--btn-gray);color:#000060;font-size:15px;font-weight:700;cursor:pointer;white-space:nowrap}
.ctl-btn.w96{width:96px}
.ctl-btn.w142{width:142px}
.ctl-btn.cross{background:rosybrown}
.ctl-btn.release{background:dodgerblue;color:#fff}
.ctl-btn:disabled{opacity:.45;cursor:default}

.modal-overlay{position:fixed;inset:0;background:rgba(0,0,0,.72);display:flex;align-items:center;justify-content:center;z-index:100}
.modal{width:min(94vw,420px);background:#d4d4d4;border:2px solid #000;padding:10px}
.modal h4{margin:0 0 8px;font-family:"Segoe UI",sans-serif;font-size:16px}
.modal .row{display:flex;flex-direction:column;gap:4px;margin-bottom:8px;font-size:13px}
.modal input,.modal select{height:34px;padding:0 8px;border:1px solid #333;background:#fff;font-size:16px}
.modal input{text-transform:uppercase}
.modal .btns{display:flex;gap:6px}
.modal button{flex:1;height:34px;border:1px solid #333;cursor:pointer;background:#b7c1c1;font-size:14px;font-weight:700}
.modal button.ok{background:#7ebf7e}
.modal button.cancel{background:#c7a7a7}
.modal .grid{display:grid;grid-template-columns:1fr 1fr;gap:6px}

.toast{position:fixed;left:50%;bottom:58px;transform:translateX(-50%);padding:6px 10px;background:rgba(0,0,0,.82);color:#fff;font-size:13px;border-radius:4px;z-index:120}

@media(max-width:840px){
  .top-menu{height:36px;min-height:36px;font-size:14px;gap:4px;overflow-x:auto;-webkit-overflow-scrolling:touch}
  .control-bar{height:auto;min-height:45px;flex-wrap:wrap}
  .strip-shell{width:calc(min(424px,100vw - 8px) * var(--strip-scale));overflow:hidden}
  .bar-shell{width:calc(min(424px,100vw - 8px) * var(--strip-scale))}
  .modal{width:min(96vw,420px)}
  .modal input,.modal select{font-size:16px}
}
@media(max-width:600px){
  .bay-btn{font-size:11px;padding:0 3px}
  .bay-btn.queue{width:auto}
  .bay-btn.div{width:auto}
  .bay-name{min-width:100px;width:100px;font-size:13px}
}

</style>
</head>
<body>
<div id="app"><div class="waiting">Connecting to OzStrips...</div></div>
<script>
(function(){
var app=document.getElementById('app');
var ws=null;
var reconn=null;
var state=null;
var selectedCS=null;
var editState=null;
var moveState=null;
var barState=null;
var runwayState=null;
var topMenuState=null;
var topMenuLeft=4;
var toastState=null;
var toastTimer=null;
var tapTimers={};
var resizeTimer=null;
var activeScrollCount=0;
var deferredRender=false;

function connect(){
  var proto=location.protocol==='https:'?'wss:':'ws:';
  ws=new WebSocket(proto+'//'+location.host+'/ws');
  ws.onopen=function(){if(reconn){clearInterval(reconn);reconn=null;}};
  ws.onmessage=function(e){
    preserveDraftValues();
    try{
      state=JSON.parse(e.data);
      if(selectedCS&&!findStripWithBay(selectedCS)){selectedCS=null;}
      if(activeScrollCount>0){deferredRender=true;}else{render();}
    }catch(_){ }
  };
  ws.onclose=function(){
    if(!reconn){
      app.innerHTML='<div class="waiting">Reconnecting...</div>';
      reconn=setInterval(connect,3000);
    }
  };
  ws.onerror=function(){try{ws.close();}catch(_){ }};
}

function send(obj){if(ws&&ws.readyState===1){ws.send(JSON.stringify(obj));}}
function esc(v){var d=document.createElement('div');d.textContent=v||'';return d.innerHTML;}

function toast(msg){
  toastState=msg;
  render();
  if(toastTimer){clearTimeout(toastTimer);}
  toastTimer=setTimeout(function(){toastState=null;render();},1800);
}

function preserveDraftValues(){
  if(editState){
    var editInput=document.getElementById('editInput');
    if(editInput){editState.value=editInput.value;}
  }
  if(barState){
    var barText=document.getElementById('barTextInput');
    var barBay=document.getElementById('barBaySelect');
    var barStyle=document.getElementById('barStyleSelect');
    if(barText){barState.text=barText.value;}
    if(barBay){barState.bay=barBay.value;}
    if(barStyle){barState.style=barStyle.value;}
    var active=document.activeElement;
    if(active){barState._focusId=active.id||null;}
  }
}

function groupedBays(){
  var bays=state&&state.Bays?state.Bays:[];
  if(!bays.length){return[];}

  var targetCols=3;
  var width=window.innerWidth||document.documentElement.clientWidth||0;
  if(width<=840){
    targetCols=1;
  }else if(width<=1250){
    targetCols=2;
  }

  if(targetCols<3){
    var compactColumns=[];
    for(var ci=0;ci<targetCols;ci++){
      compactColumns.push([]);
    }

    var maxPerColumn=Math.ceil(bays.length/targetCols);
    var colIdx=0;
    for(var bi=0;bi<bays.length;bi++){
      while(colIdx<targetCols-1&&compactColumns[colIdx].length>=maxPerColumn){
        colIdx++;
      }

      compactColumns[colIdx].push(bays[bi]);
    }

    return compactColumns.filter(function(col){return col.length>0;});
  }

  var byColumn={};
  var ordered=[];

  for(var i=0;i<bays.length;i++){
    var bay=bays[i];
    var col=(bay.Column===undefined||bay.Column===null)?i:bay.Column;
    if(!byColumn[col]){byColumn[col]=[];ordered.push(col);}
    byColumn[col].push(bay);
  }

  ordered.sort(function(a,b){return a-b;});

  var columns=[];
  for(var j=0;j<ordered.length;j++){
    columns.push(byColumn[ordered[j]]);
  }

  return columns;
}

function getBayItems(bay){
  if(bay.Items&&bay.Items.length){
    return bay.Items;
  }

  var items=[];
  var strips=bay.Strips||[];
  for(var i=0;i<strips.length;i++){
    if(bay.QueueBarIndex>=0&&i===bay.QueueBarIndex){
      items.push({ItemType:'QUEUEBAR',Text:'Queue'});
    }
    items.push({ItemType:'STRIP',Strip:strips[i]});
  }
  if(bay.QueueBarIndex>=0&&bay.QueueBarIndex>=strips.length){
    items.push({ItemType:'QUEUEBAR',Text:'Queue'});
  }
  return items;
}

function findStripWithBay(cs){
  if(!state||!state.Bays||!cs){return null;}
  for(var i=0;i<state.Bays.length;i++){
    var bay=state.Bays[i];
    var items=getBayItems(bay);
    for(var j=0;j<items.length;j++){
      if(items[j].ItemType==='STRIP'&&items[j].Strip&&items[j].Strip.Callsign===cs){
        return {strip:items[j].Strip,bay:bay};
      }
    }
  }
  return null;
}

function selectedInfo(){
  return findStripWithBay(selectedCS);
}

function runDualTap(key,singleFn,doubleFn){
  if(tapTimers[key]){
    clearTimeout(tapTimers[key]);
    delete tapTimers[key];
    doubleFn();
    return;
  }

  tapTimers[key]=setTimeout(function(){
    delete tapTimers[key];
    singleFn();
  },230);
}

function menuRow(label,cmd,checked,disabled,value){
  var attrs=' class="menu-row" data-menu-cmd="'+cmd+'"';
  if(value!==undefined&&value!==null){attrs+=' data-menu-value="'+value+'"';}
  if(disabled){attrs+=' disabled';}
  return '<button'+attrs+'><span class="menu-check">'+(checked?'&#10003;':'')+'</span><span>'+esc(label)+'</span></button>';
}

function renderTopMenuLayer(){
  if(!topMenuState){return '';}

  var h='';
  h+='<div class="menu-layer">';
  h+='<div class="menu-backdrop" id="menuBackdrop"></div>';
  h+='<div class="menu-panel" style="left:'+topMenuLeft+'px">';

  if(topMenuState==='view'){
    var layouts=state&&state.Layouts?state.Layouts:[];
    if(layouts.length>0){
      for(var li=0;li<layouts.length;li++){
        h+=menuRow(layouts[li],'setLayout',state.CurrentLayout===layouts[li],false,layouts[li]);
      }
      h+='<div class="menu-sep"></div>';
    }
    h+=menuRow('3 Columns or Less','setSmartResize',state&&state.SmartResizeMode===3,false,'3');
    h+=menuRow('2 Columns or Less','setSmartResize',state&&state.SmartResizeMode===2,false,'2');
    h+=menuRow('1 Column','setSmartResize',state&&state.SmartResizeMode===1,false,'1');
    h+=menuRow('Disabled','setSmartResize',state&&state.SmartResizeMode===0,false,'0');
    h+='<div class="menu-sep"></div>';
    h+=menuRow('Toggle Circuit Bay','toggleCircuit',state&&state.CircuitActive,!(state&&state.CircuitToggleAvailable));
    h+=menuRow('Toggle Coordinator Bay','toggleCoordinator',state&&state.CoordinatorActive,false);
    h+=menuRow('Toggle CDM','toggleCdm',state&&state.CdmEnabled,false);
  }else if(topMenuState==='settings'){
    h+=menuRow('Settings Window','openSettings',false,false);
    h+=menuRow('Keyboard Settings','openKeySettings',false,false);
    h+='<div class="menu-sep"></div>';
    h+=menuRow('PDC Sound','togglePdcSound',state&&state.PdcSoundEnabled,false);
    h+=menuRow('Inhibit Ground Map Updating','toggleGroundMaps',state&&state.GroundMapsInhibited,false);
  }else if(topMenuState==='info'){
    h+=menuRow('GitHub','openUrl',false,false,'https://github.com/maxrumsey/OzStrips/');
    h+=menuRow('Documentation','openUrl',false,false,'https://maxrumsey.xyz/OzStrips/');
    h+=menuRow('Discord Server','openUrl',false,false,'https://discord.gg/VfqFvXeg6V');
    h+=menuRow('Changelog','openUrl',false,false,'https://maxrumsey.xyz/OzStrips/changelog');
    h+=menuRow('About','showAbout',false,false);
    h+='<div class="menu-sep"></div>';
    h+=menuRow('SignalR Log','showSignalRLog',false,false);
    h+=menuRow('ReloadStrip','reloadStripElements',false,false);
    h+=menuRow('ReloadAerodromeList','reloadAerodromeList',false,false);
    h+=menuRow('Override ATIS','overrideAtis',false,false);
  }

  h+='</div>';
  h+='</div>';
  return h;
}

function render(){
  if(!state){
    app.innerHTML='<div class="waiting">Waiting for strip data...</div>';
    return;
  }

  var stripScale=Number(state.StripScale);
  if(!isFinite(stripScale)||stripScale<=0){stripScale=1;}
  if(stripScale<1){stripScale=1;}
  if(stripScale>3){stripScale=3;}
  document.documentElement.style.setProperty('--strip-scale',String(stripScale));

  var h='';
  h+='<div class="top-menu">';
  h+='<span class="label menu-item" data-menu-action="setAerodrome">Aerodrome</span>';
  h+='<span class="label menu-item '+(topMenuState==='view'?'open':'')+'" data-menu-action="view">View</span>';
  h+='<span class="label menu-item '+(topMenuState==='settings'?'open':'')+'" data-menu-action="settings">Settings</span>';
  h+='<span class="label menu-item '+(topMenuState==='info'?'open':'')+'" data-menu-action="info">Info</span>';
  h+='<span class="pending '+((state.PendingPDCs||0)>0?'active':'')+'">Pending PDCs: '+(state.PendingPDCs||0)+'</span>';
  h+='<span class="autofill '+(state.AutoFillAvailable?'on':'off')+'">Autofill Status</span>';
  h+='</div>';
  h+=renderTopMenuLayer();

  h+='<div class="main"><div class="columns">';
  var columns=groupedBays();
  for(var c=0;c<columns.length;c++){
    h+='<div class="column">';
    var bays=columns[c];
    for(var b=0;b<bays.length;b++){
      h+=renderBay(bays[b]);
    }
    h+='</div>';
  }
  h+='</div></div>';

  h+=renderControlBar();

  if(editState){h+=renderEditModal();}
  if(moveState){h+=renderMoveModal();}
  if(barState){h+=renderBarModal();}
  if(runwayState){h+=renderRunwayModal();}
  if(toastState){h+='<div class="toast">'+esc(toastState)+'</div>';}

  var scrollPositions=saveScrollPositions();
  var mainEl=app.querySelector('.main');
  var mainScroll=mainEl?{left:mainEl.scrollLeft,top:mainEl.scrollTop}:null;
  app.innerHTML=h;
  bindEvents();
  restoreModalFocus();
  restoreScrollPositions(scrollPositions);
  if(mainScroll){var newMain=app.querySelector('.main');if(newMain){newMain.scrollLeft=mainScroll.left;newMain.scrollTop=mainScroll.top;}}
}

function saveScrollPositions(){
  var positions={};
  app.querySelectorAll('[data-drop-bay]').forEach(function(el){
    var bay=el.getAttribute('data-drop-bay');
    if(el.scrollTop>0){positions[bay]=el.scrollTop;}
  });
  return positions;
}

function restoreScrollPositions(positions){
  app.querySelectorAll('[data-drop-bay]').forEach(function(el){
    var bay=el.getAttribute('data-drop-bay');
    if(positions[bay]){el.scrollTop=positions[bay];}
  });
}

function renderBay(bay){
  var h='';
  h+='<section class="bay">';
  h+='<header class="bay-top">';
  h+='<span class="bay-name">'+esc(bay.Name||'Bay')+'</span>';
  h+='<span class="bay-spacer"></span>';
  h+='<button class="bay-btn queue" data-bay-action="queue" data-bay="'+bay.Bay+'">Add to Queue</button>';
  h+='<button class="bay-btn div" data-bay-action="toggleQueue" data-bay="'+bay.Bay+'">Toggle Queue Bar</button>';
  h+='</header>';
  h+='<div class="bay-body" data-drop-bay="'+bay.Bay+'">';

  var items=getBayItems(bay);
  for(var i=0;i<items.length;i++){
    var item=items[i];
    if(item.ItemType==='STRIP'&&item.Strip){
      h+=renderStrip(item.Strip);
    }else{
      h+=renderBar(item);
    }
  }

  h+='</div></section>';
  return h;
}

function renderBar(item){
  var style=Number(item.Style||0);
  var cls='bar-item';
  if(style>0){cls+=' style'+style;}
  var text=item.Text||'';
  return '<div class="bar-shell"><div class="'+cls+'">'+esc(text)+'</div></div>';
}

function acidClass(s){
  if(selectedCS===s.Callsign||s.Picked){
    return 'bg-silver';
  }
  if(s.LastTransmit){
    return 'bg-lime';
  }
  if(s.WorldFlight){
    return 'bg-world';
  }
  return '';
}

function eobtClass(s){
  if(!s.CdmActive){
    return '';
  }
  if(s.CdmReadyToPush){
    return 'bg-limegreen';
  }
  if(s.CdmHasSlot){
    return 'bg-pink';
  }
  return 'bg-lightgray';
}

function sidClass(s){
  var cls=s.VfrSidAlert?'bg-orange':'bg-limegreen';
  if(s.SidTransition){
    cls+=' sid-transition';
  }
  return cls;
}

function pdcClass(s){
  if(!s.PdcRequested){
    return '';
  }
  return s.PdcNeedsAck?'pdc-blink':'bg-yellow';
}

function renderStrip(s){
  var typeClass=s.StripType==='ARRIVAL'?'arr':(s.StripType==='LOCAL'?'loc':(s.StripType==='DEPARTURE'?'dep':'unk'));
  var shell='strip-shell '+typeClass;
  if(s.CockLevel===1){shell+=' cock1';}
  if(s.Crossing){shell+=' crossing';}

  var cs=esc(s.Callsign||'');
  var h='';
  h+='<article class="'+shell+'" data-cs="'+cs+'">';
  h+='<div class="strip-canvas"><div class="strip-grid">';
  h+=cell('c-stand cock-zone '+(s.StandAutofilled?'bg-lightgray':''),s.Gate,'tap-gate');
  h+=cell('c-eobt cock-zone '+eobtClass(s),s.Time,'tap-eobt');
  h+=cell('c-type cock-zone',s.AircraftType,'tap-openfdr');
  h+=cell('c-wtc cock-zone',s.WTC,'');
  h+=cell('c-acid '+acidClass(s),s.Callsign,'tap-acid');
  h+=cell('c-rwy',s.Runway,'tap-rwy');
  h+=cell('c-clx',s.CLX,'tap-clx');
  h+=cell('c-sid '+sidClass(s),s.SID,'tap-sid');
  h+=cell('c-rfl '+(s.RflAlert?'bg-orange':''),s.RFL,'tap-rfl');
  h+=cell('c-ades cock-zone',s.ADES,'tap-openfdr');
  h+=cell('c-route cock-zone',s.RouteIndicator,'tap-route');
  h+=cell('c-frul cock-zone',s.FlightRules,'tap-route');
  h+=cell('c-pdc cock-zone '+pdcClass(s),s.PDCIndicator,'tap-pdc');
  h+=cell('c-ssr cock-zone '+(s.SsrAlert?'bg-orange':''),s.SSR,'tap-ssr');
  h+=cell('c-ready '+(s.ReadyAlert?'bg-orange':''),s.Ready?'RDY':'','tap-ready');
  h+=cell('c-fwpt '+(s.RouteAlert?'bg-orange':''),s.FirstWpt,'tap-fwpt');
  h+=cell('c-cfl',s.CFL,'tap-cfl');
  h+=cell('c-glop cross-zone '+(s.NoHdgAlert?'bg-orange':''),s.GLOP,'tap-glop');
  h+=cell('c-remark cross-zone',s.Remark,'tap-remark');
  h+=cell('c-freq cross-zone',s.DepartureFreq,'tap-freq');
  h+=cell('c-tot '+(s.Tot&&s.Tot!=='00:00'?'tot-running':''),s.Tot||'00:00','tap-tot');
  h+='</div></div></article>';
  return h;
}

function cell(cls,text,action){
  var ac=action?(' tap" data-action="'+action):'';
  return '<div class="cell '+cls+ac+'">'+esc(text||'')+'</div>';
}

function renderControlBar(){
  var selected=selectedInfo();
  var disableSel=selected?'':' disabled';
  var connClass='conn-stat '+(state.Connected?'connected':'disconnected')+((state.ConnectionsCount||0)>1?' multi':'');
  var runwayPairs=state&&state.RunwayPairs?state.RunwayPairs:[];
  var releaseDisabled=runwayPairs.length?'':' disabled';

  var h='';
  h+='<div class="control-bar">';
  h+='<div class="'+connClass+'"><span>CONN STAT</span></div>';
  h+='<div class="time-box">'+esc(state.UtcTime||'--:--:--')+'</div>';
  h+='<div class="ad-wrap"><div class="ad-box">'+esc(state.Aerodrome||'????')+'</div></div>';
  h+='<div class="atis-wrap"><div class="atis-box">'+esc(state.Atis||'Z')+'</div></div>';
  h+='<button class="ctl-btn w96" data-control="inhibit"'+disableSel+'>INHIBIT</button>';
  h+='<button class="ctl-btn w142 cross" data-control="crossBar">XX CROSS XX</button>';
  h+='<button class="ctl-btn w142 release" data-control="releaseBar"'+releaseDisabled+'>XX RELEASE XX</button>';
  h+='<button class="ctl-btn w96" data-control="addBar">ADD BAR</button>';
  h+='<button class="ctl-btn w96" data-control="flip"'+disableSel+'>FLIP</button>';
  h+='</div>';
  return h;
}

function renderEditModal(){
  var h='';
  h+='<div class="modal-overlay" id="editOverlay"><div class="modal">';
  h+='<h4>'+esc(editState.label)+(editState.cs?(' - '+esc(editState.cs)):'')+'</h4>';
  h+='<div class="row"><input id="editInput" value="'+esc(editState.value||'')+'" /></div>';
  h+='<div class="btns"><button class="ok" id="editOk">OK</button><button class="cancel" id="editCancel">Cancel</button></div>';
  h+='</div></div>';
  return h;
}

function renderMoveModal(){
  var h='';
  h+='<div class="modal-overlay" id="moveOverlay"><div class="modal">';
  h+='<h4>Actions - '+esc(moveState.cs)+'</h4>';
  h+='<div class="grid" style="margin-bottom:8px">';
  h+='<button data-move-action="moveUp">Move Up</button>';
  h+='<button data-move-action="moveDown">Move Down</button>';
  h+='<button data-move-action="moveToBarUp">Up To Bar</button>';
  h+='<button data-move-action="moveToBarDown">Down To Bar</button>';
  h+='<button data-move-action="crossing">Toggle Cross</button>';
  h+='<button data-move-action="flip">Flip Type</button>';
  h+='<button data-move-action="inhibitSidAlert">Inhibit SID Alert</button>';
  h+='<button data-move-action="inhibit">Inhibit Strip</button>';
  h+='</div>';
  h+='<h4>Move To Bay</h4>';
  h+='<div class="grid">';

  var bays=state&&state.Bays?state.Bays:[];
  for(var i=0;i<bays.length;i++){
    var bay=bays[i];
    h+='<button data-movebay="'+bay.Bay+'">'+esc(bay.Name)+'</button>';
  }

  h+='</div>';
  h+='<div class="btns" style="margin-top:8px"><button class="cancel" id="moveCancel">Cancel</button></div>';
  h+='</div></div>';
  return h;
}

function renderBarModal(){
  var bays=state&&state.Bays?state.Bays:[];
  var h='';
  h+='<div class="modal-overlay" id="barOverlay"><div class="modal">';
  h+='<h4>Add Bar</h4>';
  h+='<div class="row"><label>Bay</label><select id="barBaySelect">';
  for(var i=0;i<bays.length;i++){
    var sel=String(bays[i].Bay)===String(barState.bay)?' selected':'';
    h+='<option value="'+bays[i].Bay+'"'+sel+'>'+esc(bays[i].Name)+'</option>';
  }
  h+='</select></div>';
  h+='<div class="row"><label>Style</label><select id="barStyleSelect">';
  h+='<option value="1"'+(barState.style==='1'?' selected':'')+'>Grey</option>';
  h+='<option value="2"'+(barState.style==='2'?' selected':'')+'>Orange</option>';
  h+='<option value="3"'+(barState.style==='3'?' selected':'')+'>Red</option>';
  h+='</select></div>';
  h+='<div class="row"><label>Text</label><input id="barTextInput" value="'+esc(barState.text||'')+'" /></div>';
  h+='<div class="btns"><button class="ok" id="barOk">OK</button><button class="cancel" id="barCancel">Cancel</button></div>';
  h+='</div></div>';
  return h;
}

function renderRunwayModal(){
  var pairs=state&&state.RunwayPairs?state.RunwayPairs:[];
  var title=runwayState&&runwayState.mode==='release'?'Release Runway':'Crossing Runway';
  var h='';
  h+='<div class="modal-overlay" id="runwayOverlay"><div class="modal">';
  h+='<h4>'+title+'</h4>';
  h+='<div class="grid">';

  for(var i=0;i<pairs.length;i++){
    var p=String(pairs[i]||'').trim().toUpperCase();
    if(!p){continue;}
    var label=p;
    if(p.length%2===0){
      label=p.slice(0,p.length/2)+'/'+p.slice(p.length/2);
    }
    h+='<button data-runway="'+p+'">'+esc(label)+'</button>';
  }

  h+='</div>';
  h+='<div class="btns" style="margin-top:8px"><button class="cancel" id="runwayCancel">Cancel</button></div>';
  h+='</div></div>';
  return h;
}

function restoreModalFocus(){
  if(editState){
    var inp=document.getElementById('editInput');
    if(inp&&document.activeElement!==inp){inp.focus();var len=inp.value.length;inp.setSelectionRange(len,len);}
  }
  if(barState){
    var savedFocusId=barState._focusId;
    if(savedFocusId){
      var savedEl=document.getElementById(savedFocusId);
      if(savedEl){savedEl.focus();return;}
    }
    var modal=document.getElementById('barOverlay');
    var active=document.activeElement;
    var alreadyInModal=modal&&active&&modal.contains(active)&&active.tagName!=='BUTTON'&&active.tagName!=='DIV';
    if(!alreadyInModal){
      var barInp=document.getElementById('barTextInput');
      if(barInp){barInp.focus();}
    }
  }
}

function openEdit(cs,field,label,value){
  editState={cs:cs,field:field,label:label,value:value||''};
  render();
}

function bindEvents(){
  app.querySelectorAll('[data-menu-action]').forEach(function(el){
    el.addEventListener('click',function(){
      var action=el.getAttribute('data-menu-action');
      if(action==='setAerodrome'){
        var current=state&&state.Aerodrome?state.Aerodrome:'';
        topMenuState=null;
        openEdit('', 'setAerodrome', 'Set Aerodrome (ICAO)', current);
        return;
      }

      if(action==='view'||action==='settings'||action==='info'){
        var rect=el.getBoundingClientRect();
        topMenuLeft=Math.max(2,Math.floor(rect.left));
        if(topMenuState===action){
          topMenuState=null;
        }else{
          topMenuState=action;
        }
        render();
      }
    });
  });

  var menuBackdrop=document.getElementById('menuBackdrop');
  if(menuBackdrop){
    menuBackdrop.addEventListener('click',function(){
      topMenuState=null;
      render();
    });
  }

  app.querySelectorAll('[data-menu-cmd]').forEach(function(el){
    el.addEventListener('click',function(e){
      e.stopPropagation();
      var cmd=el.getAttribute('data-menu-cmd');
      var value=el.getAttribute('data-menu-value');
      topMenuState=null;

      if(cmd==='openUrl'){
        if(value){window.open(value,'_blank','noopener,noreferrer');}
        render();
        return;
      }

      var payload={Action:cmd};
      if(value!==null&&value!==undefined&&value!==''){
        payload.Value=value;
      }
      send(payload);
      render();
    });
  });

  app.querySelectorAll('[data-action]').forEach(function(el){
    el.addEventListener('click',function(e){
      e.stopPropagation();
      var bayBody=el.closest('.bay-body');
      if(bayBody&&bayBody._touchScrolled){return;}
      var shell=el.closest('.strip-shell');
      var cs=shell?shell.getAttribute('data-cs'):null;
      if(!cs){return;}
      var info=findStripWithBay(cs);
      if(!info||!info.strip){return;}

      var s=info.strip;
      var action=el.getAttribute('data-action');

      switch(action){
        case 'tap-acid':
          if(selectedCS===cs){
            moveState={cs:cs};
          }else{
            selectedCS=cs;
          }
          render();
          break;
        case 'tap-eobt':
          runDualTap(cs+'-eobt',function(){
            send({Action:'cock',Callsign:cs});
          },function(){
            send({Action:'openCdm',Callsign:cs});
          });
          break;
        case 'tap-openfdr':
          send({Action:'openFdr',Callsign:cs});
          break;
        case 'tap-route':
          runDualTap(cs+'-route',function(){
            send({Action:'showRoute',Callsign:cs});
          },function(){
            send({Action:'inhibitAlert',Callsign:cs,Value:'ROUTE'});
          });
          break;
        case 'tap-pdc':
          runDualTap(cs+'-pdc',function(){
            send({Action:'openPdc',Callsign:cs});
          },function(){
            send({Action:'openPm',Callsign:cs});
          });
          break;
        case 'tap-fwpt':
          runDualTap(cs+'-fwpt',function(){
            send({Action:'openFdr',Callsign:cs});
          },function(){
            send({Action:'openReroute',Callsign:cs});
          });
          break;
        case 'tap-ready':
          runDualTap(cs+'-ready',function(){
            send({Action:'ready',Callsign:cs});
          },function(){
            send({Action:'inhibitAlert',Callsign:cs,Value:'READY'});
          });
          break;
        case 'tap-clx':
          openEdit(cs,'setClx','Clearance',s.CLX||'');
          break;
        case 'tap-remark':
          openEdit(cs,'setRemark','Remark',s.Remark||'');
          break;
        case 'tap-gate':
          openEdit(cs,'setGate','Gate/Stand',s.Gate||'');
          break;
        case 'tap-freq':
          openEdit(cs,'setFreq','Dep Frequency',s.DepartureFreq||'');
          break;
        case 'tap-rwy':
          openEdit(cs,'setRwy','Runway',s.Runway||'');
          break;
        case 'tap-cfl':
          openEdit(cs,'setCfl','CFL',s.CFL||'');
          break;
        case 'tap-glop':
          runDualTap(cs+'-glop',function(){
            openEdit(cs,'setGlop','Global Ops',s.GLOP||'');
          },function(){
            send({Action:'inhibitAlert',Callsign:cs,Value:'HDG'});
          });
          break;
        case 'tap-ssr':
          runDualTap(cs+'-ssr',function(){
            send({Action:'assignSsr',Callsign:cs});
          },function(){
            send({Action:'inhibitAlert',Callsign:cs,Value:'SSR'});
          });
          break;
        case 'tap-sid':
          runDualTap(cs+'-sid',function(){
            send({Action:'sidTrigger',Callsign:cs});
          },function(){
            openEdit(cs,'setSid','SID',s.SID||'');
          });
          break;
        case 'tap-rfl':
          runDualTap(cs+'-rfl',function(){
            send({Action:'openFdr',Callsign:cs});
          },function(){
            send({Action:'inhibitAlert',Callsign:cs,Value:'RFL'});
          });
          break;
        case 'tap-tot':
          send({Action:'toggleTot',Callsign:cs});
          break;
      }
    });
  });

  app.querySelectorAll('[data-bay-action]').forEach(function(el){
    el.addEventListener('click',function(){
      var bay=parseInt(el.getAttribute('data-bay'),10);
      var action=el.getAttribute('data-bay-action');

      if(action==='toggleQueue'){
        send({Action:'toggleQueueBar',Bay:bay});
        return;
      }

      if(action==='queue'){
        var info=selectedInfo();
        if(!info||!selectedCS){
          toast('Select a strip first.');
          return;
        }

        if(Number(info.bay.Bay)!==bay){
          toast('Selected strip must be in this bay.');
          return;
        }

        send({Action:'queueUp',Callsign:selectedCS});
      }
    });
  });

  app.querySelectorAll('[data-drop-bay]').forEach(function(el){
    el.addEventListener('click',function(e){
      if(el._touchScrolled){return;}
      var target=e.target;
      if(!target||typeof target.closest!=='function'){return;}
      if(target&&target.closest('.strip-shell')){return;}
      if(target&&target.closest('.bar-item')){return;}
      if(target&&target.closest('.bay-btn')){return;}

      if(!selectedCS){
        return;
      }

      var bay=parseInt(el.getAttribute('data-drop-bay'),10);
      send({Action:'move',Callsign:selectedCS,Bay:bay});
    });
  });

  app.querySelectorAll('[data-control]').forEach(function(el){
    el.addEventListener('click',function(){
      var action=el.getAttribute('data-control');
      var info=selectedInfo();

      if(action==='inhibit'){
        if(!info||!selectedCS){toast('Select a strip first.');return;}
        send({Action:'inhibit',Callsign:selectedCS});
        return;
      }

      if(action==='flip'){
        if(!info||!selectedCS){toast('Select a strip first.');return;}
        send({Action:'flip',Callsign:selectedCS});
        return;
      }

      if(action==='crossBar'){
        var crossPairs=state&&state.RunwayPairs?state.RunwayPairs:[];
        if(crossPairs.length){
          runwayState={mode:'cross'};
          render();
          return;
        }
        send({Action:'toggleCrossBarDirect'});
        return;
      }

      if(action==='releaseBar'){
        var releasePairs=state&&state.RunwayPairs?state.RunwayPairs:[];
        if(!releasePairs.length){
          toast('Release bar unavailable for this aerodrome.');
          return;
        }
        runwayState={mode:'release'};
        render();
        return;
      }

      if(action==='addBar'){
        var bays=state&&state.Bays?state.Bays:[];
        if(!bays.length){toast('No bays available.');return;}
        barState={bay:String(bays[0].Bay),style:'1',text:''};
        render();
      }
    });
  });

  var editOk=document.getElementById('editOk');
  if(editOk){
    editOk.addEventListener('click',function(){
      if(!editState){return;}
      var input=document.getElementById('editInput');
      var val=input?input.value:'';
      if(editState.field==='setAerodrome'){
        var aerodrome=String(val||'').trim().toUpperCase();
        if(aerodrome.length!==4){
          toast('Enter a 4-letter ICAO code.');
          return;
        }

        send({Action:'setAerodrome',Value:aerodrome});
      }else{
        send({Action:editState.field,Callsign:editState.cs,Value:String(val||'').toUpperCase()});
      }
      editState=null;
      render();
    });
  }

  var editCancel=document.getElementById('editCancel');
  if(editCancel){editCancel.addEventListener('click',function(){editState=null;render();});}
  var editOverlay=document.getElementById('editOverlay');
  if(editOverlay){editOverlay.addEventListener('click',function(e){if(e.target===editOverlay){editState=null;render();}});}

  var moveCancel=document.getElementById('moveCancel');
  if(moveCancel){moveCancel.addEventListener('click',function(){moveState=null;render();});}
  var moveOverlay=document.getElementById('moveOverlay');
  if(moveOverlay){moveOverlay.addEventListener('click',function(e){if(e.target===moveOverlay){moveState=null;render();}});}

  app.querySelectorAll('[data-movebay]').forEach(function(el){
    el.addEventListener('click',function(){
      if(!moveState){return;}
      var bay=parseInt(el.getAttribute('data-movebay'),10);
      send({Action:'move',Callsign:moveState.cs,Bay:bay});
      moveState=null;
      render();
    });
  });

  app.querySelectorAll('[data-move-action]').forEach(function(el){
    el.addEventListener('click',function(){
      if(!moveState){return;}
      var action=el.getAttribute('data-move-action');
      send({Action:action,Callsign:moveState.cs});
      moveState=null;
      render();
    });
  });

  var barOk=document.getElementById('barOk');
  if(barOk){
    barOk.addEventListener('click',function(){
      if(!barState){return;}
      var textEl=document.getElementById('barTextInput');
      var bayEl=document.getElementById('barBaySelect');
      var styleEl=document.getElementById('barStyleSelect');

      var text=textEl?textEl.value:'';
      if(!text||!text.trim()){
        toast('Enter bar text.');
        return;
      }

      send({
        Action:'addBar',
        Bay:parseInt(bayEl?bayEl.value:barState.bay,10),
        Style:parseInt(styleEl?styleEl.value:barState.style,10),
        Value:text.toUpperCase(),
      });

      barState=null;
      render();
    });
  }

  var barCancel=document.getElementById('barCancel');
  if(barCancel){barCancel.addEventListener('click',function(){barState=null;render();});}
  var barOverlay=document.getElementById('barOverlay');
  if(barOverlay){barOverlay.addEventListener('click',function(e){if(e.target===barOverlay){barState=null;render();}});}

  var runwayCancel=document.getElementById('runwayCancel');
  if(runwayCancel){runwayCancel.addEventListener('click',function(){runwayState=null;render();});}
  var runwayOverlay=document.getElementById('runwayOverlay');
  if(runwayOverlay){runwayOverlay.addEventListener('click',function(e){if(e.target===runwayOverlay){runwayState=null;render();}});}
  app.querySelectorAll('[data-runway]').forEach(function(el){
    el.addEventListener('click',function(){
      if(!runwayState){return;}
      var pair=String(el.getAttribute('data-runway')||'').trim().toUpperCase();
      if(!pair){return;}
      if(runwayState.mode==='release'){
        send({Action:'toggleReleaseBar',Value:pair});
      }else{
        send({Action:'toggleCrossBar',Value:pair});
      }
      runwayState=null;
      render();
    });
  });

  app.querySelectorAll('.bay-body').forEach(function(el){
    el.addEventListener('pointerdown',function(e){
      if(e.pointerType!=='touch'){return;}
      if(el._fling){cancelAnimationFrame(el._fling);el._fling=null;}
      activeScrollCount++;
      el.setPointerCapture(e.pointerId);
      el._touchScrolled=false;
      el._scrollData={id:e.pointerId,lastY:e.clientY,pendingDy:0,raf:null,samples:[],tracking:true,totalMove:0};
    });
    el.addEventListener('pointermove',function(e){
      var sd=el._scrollData;
      if(!sd||!sd.tracking||e.pointerId!==sd.id){return;}
      e.preventDefault();
      var dy=sd.lastY-e.clientY;
      sd.lastY=e.clientY;
      sd.pendingDy+=dy;
      sd.totalMove+=Math.abs(dy);
      if(sd.totalMove>8){el._touchScrolled=true;}
      sd.samples.push({dy:dy,t:Date.now()});
      if(sd.samples.length>6){sd.samples.shift();}
      if(!sd.raf){
        sd.raf=requestAnimationFrame(function(){
          if(sd.pendingDy!==0){el.scrollTop+=sd.pendingDy;sd.pendingDy=0;}
          sd.raf=null;
        });
      }
    });
    el.addEventListener('pointerup',function(e){
      var sd=el._scrollData;
      if(!sd||e.pointerId!==sd.id){return;}
      sd.tracking=false;
      if(sd.raf){cancelAnimationFrame(sd.raf);sd.raf=null;}
      if(sd.pendingDy!==0){el.scrollTop+=sd.pendingDy;sd.pendingDy=0;}
      var vy=0;
      var now=Date.now();
      var recent=sd.samples.filter(function(s){return now-s.t<80;});
      if(recent.length>=2){
        var totalDy=0;var totalDt=recent[recent.length-1].t-recent[0].t;
        for(var i=0;i<recent.length;i++){totalDy+=recent[i].dy;}
        if(totalDt>0){vy=totalDy/totalDt;}
      }
      el._scrollData=null;
      activeScrollCount=Math.max(0,activeScrollCount-1);
      if(el._touchScrolled){setTimeout(function(){el._touchScrolled=false;},300);}
      if(Math.abs(vy)<0.15){
        if(activeScrollCount===0&&deferredRender){deferredRender=false;render();}
        return;
      }
      var velocity=vy*1000;
      var friction=0.95;
      var lastT=performance.now();
      function flingStep(t){
        var dt=(t-lastT)/16.667;
        lastT=t;
        velocity*=Math.pow(friction,dt);
        if(Math.abs(velocity)<10){el._fling=null;if(activeScrollCount===0&&deferredRender){deferredRender=false;render();}return;}
        el.scrollTop+=velocity*(dt*16.667/1000);
        el._fling=requestAnimationFrame(flingStep);
      }
      el._fling=requestAnimationFrame(flingStep);
    });
    el.addEventListener('pointercancel',function(e){
      var sd=el._scrollData;
      if(sd&&e.pointerId===sd.id){
        if(sd.raf){cancelAnimationFrame(sd.raf);}
        el._scrollData=null;
        activeScrollCount=Math.max(0,activeScrollCount-1);
        if(activeScrollCount===0&&deferredRender){deferredRender=false;render();}
      }
    });
  });

  var editInput=document.getElementById('editInput');
  if(editInput){
    editInput.addEventListener('keydown',function(e){
      if(e.key==='Enter'){var ok=document.getElementById('editOk');if(ok){ok.click();}}
      if(e.key==='Escape'){editState=null;render();}
    });
  }

}

connect();

window.addEventListener('resize',function(){
  if(!state){return;}
  if(resizeTimer){clearTimeout(resizeTimer);}
  resizeTimer=setTimeout(function(){
    preserveDraftValues();
    render();
  },90);
});
})();
</script>
</body>
</html>
""";
}
