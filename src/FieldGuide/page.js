// Field Guide's web half. Runs in Root.html, the document every game page iframe sits in. Draws a floating window
// over all of them that shows the Survival Log Database and can be dragged by its title bar and resized from any edge
// or corner, puts a button in the HUD's tool column built like the game's own buttons there, adds a Field Guide app to
// the phone's home screen, and on the hotkey opens the page for whatever the pointer is over. Talks to the plugin
// through vuplex.postMessage("slmod|fieldguide|<verb>") and is driven by window.__slfieldguide.
(function () {
  if (window.__slfieldguide) return;

  var PREFIX = 'slmod|fieldguide|';
  var HUD_CLASS = 'fg-hud-btn';
  // The guide's place in the HUD's tool column when Exiled's Toolkit lays it out in columns of four.
  var DOCK_ORDER = '21';
  var PHONE_APP_CLASS = 'fg-app';
  var PHONE_VIEW_CLASS = 'fg-phone';
  var PHONE_STYLE_ID = 'fg-phone-style';
  var MIN_W = 0.15;
  var MIN_H = 0.2;
  // The site's text size in the window, in percent (TextSize.cs).
  var TEXT_MIN = 50;
  var TEXT_MAX = 150;
  var TEXT_STEP = 10;
  // The site shows only its name and search box in a frame with this name, since the window has its own Home.
  var FRAME_NAME = 'field-guide';
  var PHONE_FRAME_NAME = 'field-guide-phone';
  var GRIPS = ['n', 's', 'e', 'w', 'ne', 'nw', 'se', 'sw'];
  var SVG_NS = 'http://www.w3.org/2000/svg';
  // An open book, drawn on the 24-unit grid the game's HUD icons use; filled, holes by the even-odd rule.
  var BOOK_FILL = 'M2 4h7c1.2 0 2.3.5 3 1.3.7-.8 1.8-1.3 3-1.3h7v15h-7c-1.1 0-2 .9-2 2h-2c0-1.1-.9-2-2-2H2V4zm2 2v11h5c.7 0 1.4.2 2 .5V7.5c0-.8-.7-1.5-1.5-1.5H4zm9 1.5v10c.6-.3 1.3-.5 2-.5h5V6h-5.5c-.8 0-1.5.7-1.5 1.5z';
  // The same book as two strokes, for the phone's app icons, which are drawn in outline.
  var BOOK_STROKES = ['M3 5h6a3 3 0 0 1 3 3v12a2 2 0 0 0-2-2H3z', 'M21 5h-6a3 3 0 0 0-3 3v12a2 2 0 0 1 2-2h7z'];
  // The window buttons, drawn on a 10-unit grid the way a desktop window draws them.
  var ICONS = {
    minimize: 'M1.5 8.5h7',
    maximize: 'M1.5 1.5h7v7h-7z',
    restore: 'M3.5 1.5h5v5M1.5 3.5h5v5h-5z',
    expand: 'M6 1.5h2.5V4M8.5 1.5L5.5 4.5M4 8.5H1.5V6M1.5 8.5l3-3'
  };
  // The title bar's height with the window's border, which is all a minimized window keeps.
  var BAR_PX = 22;
  // The site names the page it is on in a message to the game's root page (src/designs/depot/Frame.astro).
  var SITE_AT = 'slfg-at|';
  // The plugin hands over the words in the game's language before every call (Texts.cs); English stands in until then.
  var ENGLISH = {
    title: 'Field Guide', tip: 'The Survival Log Database. Hover an item or a map location and press {0} to look it up',
    home: 'Home', close: 'Close', back: 'Back', smaller: 'Smaller text', bigger: 'Larger text',
    minimize: 'Minimize', maximize: 'Maximize', restore: 'Restore', expand: 'Open in the large window',
    needCopy: 'Source is set to Offline and there is no offline copy yet. Download it here, or set Source to Auto to read the site online.'
  };

  var view = { base: null, lang: 'en', layout: { x: 0.6, y: 0.08, w: 0.36, h: 0.8 }, textSize: 80, source: null, action: null, canDownload: false, hotkey: '/' };
  var hasView = false;
  var rect = null;
  var textSize = null;
  var ui = null;
  var drag = null;
  var isOpen = false;
  var isMaximized = false;
  var isMinimized = false;
  var path = null;
  var phone = { isOpen: false, path: null };
  var hasReportedPageAccess = false;
  var hasReportedHudPlaced = false;
  var hasReportedPhonePlaced = false;

  function say(key) {
    var words = window.__slfieldguideWords || ENGLISH;
    var text = words[key] || ENGLISH[key] || key;

    for (var i = 1; i < arguments.length; i++) text = text.split('{' + (i - 1) + '}').join(String(arguments[i]));

    return text;
  }

  function send(message) {
    try {
      if (window.vuplex) window.vuplex.postMessage(PREFIX + message);
    } catch (e) {}
  }

  function make(doc, tag, className, text) {
    var node = doc.createElement(tag);

    if (className) node.className = className;
    if (text != null) node.textContent = text;

    return node;
  }

  function clamp(value, low, high) {
    return Math.min(Math.max(value, low), high);
  }

  function round(value) {
    return String(Math.round(value * 10000) / 10000);
  }

  function rectText(r) {
    return [r.x, r.y, r.w, r.h].map(round).join(',');
  }

  // The live site's analytics count a visit as the Field Guide's by its utm tags; the offline copy has no analytics.
  function siteAddress(to, medium) {
    if (!view.campaign) return view.base + to;

    var joiner = to.indexOf('?') < 0 ? '?' : '&';

    return view.base + to + joiner + view.campaign + '&utm_medium=' + medium;
  }

  function homePath() {
    return '/' + view.lang + '/';
  }

  // A stroked icon in the current color, for a window button.
  function drawIcon(button, name) {
    var doc = button.ownerDocument;
    var svg = doc.createElementNS(SVG_NS, 'svg');
    var line = doc.createElementNS(SVG_NS, 'path');

    svg.setAttribute('viewBox', '0 0 10 10');
    svg.setAttribute('class', 'fg-icon');
    line.setAttribute('d', ICONS[name]);
    svg.appendChild(line);
    button.textContent = '';
    button.appendChild(svg);
    button.setAttribute('data-icon', name);
  }

  // The site's address for the page it says it is on, without the analytics tags the guide added.
  function pageFromSite(at) {
    var parts = at.split('?');
    var kept = (parts[1] || '').split('&').filter(function (pair) { return pair && pair.indexOf('utm_') !== 0; });

    return parts[0] + (kept.length ? '?' + kept.join('&') : '');
  }

  // "slfg-at|<frame name>|<path>" from a site page, so the guide knows where the reader is when it reloads or moves
  // the page. Only plain site paths are taken.
  function onSiteMessage(e) {
    var data = e && typeof e.data === 'string' ? e.data : '';

    if (data.indexOf(SITE_AT) !== 0) return;

    var parts = data.substring(SITE_AT.length).split('|');
    var at = parts[1] || '';

    if (at.charAt(0) !== '/' || at.charAt(1) === '/') return;
    if (parts[0] === FRAME_NAME && isOpen) path = pageFromSite(at);
    else if (parts[0] === PHONE_FRAME_NAME && phone.screen) phone.at = pageFromSite(at);
  }

  // ===== The window =====

  var CSS = [
    '#fg-win{position:fixed;z-index:2147483000;display:none;flex-direction:column;background:#16130f;border:1px solid rgba(251,176,52,.55);border-radius:6px;box-shadow:0 8px 28px rgba(0,0,0,.6);overflow:hidden;font-family:inherit}',
    '#fg-win.is-open{display:flex}',
    '.fg-bar{flex:none;display:flex;align-items:center;gap:4px;height:20px;padding:0 2px 0 8px;background:#2a2016;color:#fbb034;cursor:move;user-select:none;font-size:11px}',
    '.fg-title{font-weight:bold;white-space:nowrap}',
    '.fg-chip{font-size:9px;line-height:12px;color:#c8b48a;border:1px solid rgba(251,176,52,.35);border-radius:7px;padding:0 5px;white-space:nowrap}',
    '.fg-gap{flex:1}',
    '.fg-btn{background:transparent;color:#fbb034;border:1px solid rgba(251,176,52,.45);border-radius:3px;padding:0 5px;font-size:10px;line-height:14px;cursor:pointer;white-space:nowrap}',
    '.fg-btn:hover{background:rgba(251,176,52,.15)}',
    '.fg-btn:disabled{opacity:.6;cursor:default}',
    '.fg-x{border:none;font-size:14px;line-height:14px;padding:0 5px}',
    '.fg-win-btn{border:none;padding:2px 4px;line-height:0}',
    '.fg-icon{width:10px;height:10px;fill:none;stroke:currentColor;stroke-width:1.2}',
    '#fg-win.is-min .fg-body,#fg-win.is-min .fg-grip,#fg-win.is-min .fg-gap,#fg-win.is-max .fg-grip{display:none}',
    '#fg-win.is-max{border-radius:0}',
    '#fg-win.is-max .fg-bar{cursor:default}',
    '.fg-body{flex:1;position:relative;background:#16130f}',
    '.fg-frame{position:absolute;left:0;top:0;width:100%;height:100%;border:0;background:#16130f;transform-origin:0 0}',
    '.fg-empty{position:absolute;left:0;top:0;right:0;bottom:0;display:none;flex-direction:column;align-items:center;justify-content:center;gap:12px;padding:24px;color:#c8c8c8;font-size:14px;text-align:center}',
    '.fg-body.is-empty .fg-empty{display:flex}',
    '.fg-body.is-empty .fg-frame{display:none}',
    '.fg-grip{position:absolute;z-index:2}',
    '.fg-n,.fg-s{left:10px;right:10px;height:6px;cursor:ns-resize}',
    '.fg-n{top:0}.fg-s{bottom:0}',
    '.fg-e,.fg-w{top:10px;bottom:10px;width:6px;cursor:ew-resize}',
    '.fg-e{right:0}.fg-w{left:0}',
    '.fg-ne,.fg-nw,.fg-se,.fg-sw{width:12px;height:12px}',
    '.fg-ne{top:0;right:0;cursor:nesw-resize}.fg-sw{bottom:0;left:0;cursor:nesw-resize}',
    '.fg-nw{top:0;left:0;cursor:nwse-resize}.fg-se{bottom:0;right:0;cursor:nwse-resize}',
    '#fg-shield{position:fixed;left:0;top:0;right:0;bottom:0;z-index:2147483001;display:none}'
  ].join('\n');

  function build() {
    if (ui && document.body.contains(ui.root)) return;

    addStyle();

    var root = make(document, 'div');
    var bar = make(document, 'div', 'fg-bar');
    var body = make(document, 'div', 'fg-body');
    var frame = make(document, 'iframe', 'fg-frame');
    var empty = make(document, 'div', 'fg-empty');
    var shield = make(document, 'div');

    root.id = 'fg-win';
    frame.name = FRAME_NAME;
    shield.id = 'fg-shield';
    bar.appendChild(make(document, 'span', 'fg-title'));
    bar.appendChild(make(document, 'span', 'fg-chip'));
    bar.appendChild(actButton('download', 'fg-btn fg-get'));
    bar.appendChild(make(document, 'span', 'fg-gap'));
    bar.appendChild(actButton('smaller', 'fg-btn fg-smaller', 'A-'));
    bar.appendChild(actButton('bigger', 'fg-btn fg-bigger', 'A+'));
    bar.appendChild(actButton('home', 'fg-btn fg-home'));
    bar.appendChild(actButton('minimize', 'fg-btn fg-win-btn fg-min'));
    bar.appendChild(actButton('maximize', 'fg-btn fg-win-btn fg-max'));
    bar.appendChild(actButton('close', 'fg-btn fg-x', '\u00d7'));
    empty.appendChild(make(document, 'div', 'fg-empty-text'));
    empty.appendChild(actButton('download', 'fg-btn fg-get'));
    body.appendChild(frame);
    body.appendChild(empty);
    root.appendChild(bar);
    root.appendChild(body);
    GRIPS.forEach(function (kind) {
      var grip = make(document, 'div', 'fg-grip fg-' + kind);

      grip.setAttribute('data-grip', kind);
      root.appendChild(grip);
    });

    document.body.appendChild(root);
    document.body.appendChild(shield);

    root.addEventListener('click', onClick);
    root.addEventListener('mousedown', onMouseDown);
    document.addEventListener('mousemove', onMouseMove);
    document.addEventListener('mouseup', endDrag);

    ui = { root: root, bar: bar, body: body, frame: frame, shield: shield };
    path = null;
  }

  function actButton(act, className, text) {
    var node = make(document, 'button', className, text);

    node.setAttribute('data-act', act);

    return node;
  }

  // The style outlives a lost window, so a rebuild must not stack another copy of it.
  function addStyle() {
    if (document.getElementById('fg-style')) return;

    var style = make(document, 'style');

    style.id = 'fg-style';
    style.textContent = CSS;
    document.head.appendChild(style);
  }

  function render() {
    if (!ui) return;

    var chip = ui.root.querySelector('.fg-chip');
    var gets = ui.root.querySelectorAll('.fg-get');

    ui.root.querySelector('.fg-title').textContent = say('title');
    ui.root.querySelector('.fg-home').textContent = say('home');
    ui.root.querySelector('.fg-x').title = say('close');
    renderWindowButtons();
    ui.root.querySelector('.fg-smaller').title = say('smaller');
    ui.root.querySelector('.fg-bigger').title = say('bigger');
    ui.root.querySelector('.fg-empty-text').textContent = say('needCopy');
    chip.textContent = view.source || '';
    chip.style.display = view.source ? '' : 'none';
    gets.forEach(function (node) {
      node.textContent = view.action || '';
      node.style.display = view.action ? '' : 'none';
      node.disabled = !view.canDownload;
    });
    ui.body.className = view.base ? 'fg-body' : 'fg-body is-empty';
    scaleFrame();

    // A minimized bar is as wide as its buttons, and their text can change while it is down.
    if (isMinimized) report();
  }

  function renderWindowButtons() {
    var min = ui.root.querySelector('.fg-min');
    var max = ui.root.querySelector('.fg-max');

    drawIcon(min, isMinimized ? 'restore' : 'minimize');
    drawIcon(max, isMaximized ? 'restore' : 'maximize');
    min.title = say(isMinimized ? 'restore' : 'minimize');
    max.title = say(isMaximized ? 'restore' : 'maximize');
    ui.root.classList[isMinimized ? 'add' : 'remove']('is-min');
    ui.root.classList[isMaximized ? 'add' : 'remove']('is-max');
  }

  // The frame is laid out larger and shrunk back to the window, so the site reflows to the size it is read at.
  function scaleFrame() {
    var scale = textSize / 100;
    var style = ui.frame.style;

    style.width = round(100 / scale) + '%';
    style.height = round(100 / scale) + '%';
    style.transform = 'scale(' + round(scale) + ')';
  }

  function resizeText(step) {
    var next = clamp(textSize + step, TEXT_MIN, TEXT_MAX);

    if (next === textSize) return;

    textSize = next;
    scaleFrame();
    send('textsize|' + textSize);
  }

  // A minimized window shrinks to its title bar's buttons and keeps its right edge, so restore stays under the pointer.
  function place() {
    var style = ui.root.style;
    var shown = isMaximized ? { x: 0, y: 0, w: 1, h: 1 } : rect;

    style.top = shown.y * 100 + 'vh';

    if (isMinimized) {
      style.left = 'auto';
      style.right = (1 - shown.x - shown.w) * 100 + 'vw';
      style.width = 'auto';
      style.height = BAR_PX + 'px';
      return;
    }

    style.right = 'auto';
    style.left = shown.x * 100 + 'vw';
    style.width = shown.w * 100 + 'vw';
    style.height = shown.h * 100 + 'vh';
  }

  // The bar's own width once it has shrunk; before the page has laid it out, the window's width.
  function barWidth(shown) {
    var px = ui ? ui.root.offsetWidth : 0;

    return px > 0 ? Math.min(px / window.innerWidth, shown.w) : shown.w;
  }

  // A window minimized near an edge is moved on restore until all of it shows, and that place is saved.
  function fitOnScreen() {
    if (!rect) return;

    var left = clamp(rect.x, 0, 1 - rect.w);
    var top = Math.min(rect.y, 1 - rect.h);

    if (left === rect.x && top === rect.y) return;

    rect = { x: left, y: top, w: rect.w, h: rect.h };
    send('moved|' + rectText(rect));
  }

  // Where the window is on screen: the whole screen when maximized, its title bar alone when minimized.
  function screenRect() {
    var shown = isMaximized ? { x: 0, y: 0, w: 1, h: 1 } : rect;

    if (!isMinimized) return shown;

    var w = barWidth(shown);

    return { x: shown.x + shown.w - w, y: shown.y, w: w, h: Math.min(BAR_PX / window.innerHeight, shown.h) };
  }

  // Maximize and minimize change only how the window is shown, never the place the player saved.
  function setShape(maximized, minimized) {
    var wasMinimized = isMinimized;

    isMaximized = maximized;
    isMinimized = minimized;

    if (wasMinimized && !minimized) fitOnScreen();

    renderWindowButtons();
    place();
    report();
  }

  function navigate(to) {
    path = to;

    if (view.base) ui.frame.src = siteAddress(to, 'window');
  }

  function open(to) {
    build();
    render();
    place();

    if (to || !path) navigate(to || homePath());

    ui.root.classList.add('is-open');
    isOpen = true;
    setShape(isMaximized, false);
  }

  // Closing keeps the page and whether the window was maximized, so the next open picks up where the player was.
  function close() {
    if (ui) ui.root.classList.remove('is-open');

    endDrag();
    if (isMinimized) fitOnScreen();
    isOpen = false;
    isMinimized = false;
    send('hidden');
  }

  // The hotkey and the HUD button bring a minimized window back rather than closing it.
  function toggle() {
    if (isOpen && isMinimized) setShape(isMaximized, false);
    else if (isOpen) close();
    else open(null);
  }

  // The plugin keeps clicks over the window for the page only while it hears where the window is.
  function report() {
    if (isOpen && !drag) send('shown|' + rectText(screenRect()));
  }

  function onClick(e) {
    var target = e.target.closest('[data-act]');

    if (!target) return;

    e.stopPropagation();

    if (target.disabled) return;

    var act = target.getAttribute('data-act');

    if (act === 'close') close();
    else if (act === 'minimize') setShape(isMaximized, !isMinimized);
    else if (act === 'maximize') setShape(!isMaximized, false);
    else if (act === 'home') navigate(homePath());
    else if (act === 'smaller') resizeText(-TEXT_STEP);
    else if (act === 'bigger') resizeText(TEXT_STEP);
    else if (act === 'download') send('download');
  }

  // A press on the title bar moves the window, on a grip resizes it. The shield covers the whole page until the
  // button comes up, so the pointer can cross the game's own page frames without them taking the drag.
  function onMouseDown(e) {
    e.stopPropagation();

    if (e.button !== 0 || e.target.closest('[data-act]')) return;

    var grip = e.target.getAttribute('data-grip');
    var kind = grip || (e.target.closest('.fg-bar') ? 'move' : null);

    if (!kind || isMaximized) return;

    e.preventDefault();
    drag = { kind: kind, x: e.clientX, y: e.clientY, from: rect };
    ui.shield.style.cursor = kind === 'move' ? 'move' : getComputedStyle(e.target).cursor;
    ui.shield.style.display = 'block';
    send('dragging');
  }

  function onMouseMove(e) {
    if (!drag) return;

    rect = dragRect(drag.from, drag.kind, (e.clientX - drag.x) / window.innerWidth, (e.clientY - drag.y) / window.innerHeight);
    place();
  }

  function endDrag() {
    if (!drag) return;

    drag = null;
    ui.shield.style.display = 'none';
    send('moved|' + rectText(rect));
    report();
  }

  // Moves keep the size; resizes move only the edges named, never below the minimum size or off the screen.
  function dragRect(from, kind, dx, dy) {
    // Only what shows has to stay on screen: a minimized bar sits at the window's right edge.
    if (kind === 'move') {
      var shown = screenRect();

      return { x: clamp(from.x + dx, isMinimized ? shown.w - from.w : 0, 1 - from.w), y: clamp(from.y + dy, 0, 1 - shown.h), w: from.w, h: from.h };
    }

    var left = from.x;
    var top = from.y;
    var right = from.x + from.w;
    var bottom = from.y + from.h;

    if (kind.indexOf('w') >= 0) left = clamp(left + dx, 0, right - MIN_W);
    if (kind.indexOf('e') >= 0) right = clamp(right + dx, left + MIN_W, 1);
    if (kind.indexOf('n') >= 0) top = clamp(top + dy, 0, bottom - MIN_H);
    if (kind.indexOf('s') >= 0) bottom = clamp(bottom + dy, top + MIN_H, 1);

    return { x: left, y: top, w: right - left, h: bottom - top };
  }

  // ===== What the plugin calls =====

  // A new view keeps the window where the player put it; the saved place only applies before the first one.
  function setup(next) {
    var hadBase = view.base;

    view = next;

    if (!hasView || !rect) rect = next.layout;
    if (!hasView || textSize === null) textSize = next.textSize;

    hasView = true;
    render();

    if (ui && path && next.base && next.base !== hadBase) navigate(path);

    refreshPhone();
  }

  // The page for what the pointer is over, or the window toggled when it is over nothing the site knows.
  function lookup() {
    var hit = hovered();

    if (!hit) {
      toggle();

      return;
    }

    open('/' + view.lang + '/go/?' + hit.kind + '=' + hit.id);
  }

  // ===== What the pointer is over =====

  function vueSetup(doc) {
    var app = doc.getElementById('app');

    return app && app._vnode && app._vnode.component ? app._vnode.component.setupState : null;
  }

  // The backpack's item tooltip names the item's config id.
  function backpackHit(doc) {
    var state = vueSetup(doc);
    var hover = state && state.tooltipState;
    var item = hover && hover.visible ? hover.item : null;

    return item && item.cfgId ? { kind: 'item', id: item.cfgId } : null;
  }

  // Shelf items carry their config id as id; the trader's other items and the bag's are named by instance id, which
  // only some carry a config id beside.
  function tradeHit(doc) {
    var state = vueSetup(doc);
    var hover = state && state.tooltipState;
    var item = hover && hover.visible ? hover.item : null;

    if (!item || state.kinTipText || item.dimTip) return null;

    var shelf = state.shelf && state.shelf.items ? state.shelf.items : [];
    var id = shelf.indexOf(item) >= 0 ? item.id : item.cfgId;

    return id ? { kind: 'item', id: id } : null;
  }

  // Map markers are drawn one per location in the page's list (the explore map leaves out the hidden ones), so the
  // marker under the pointer is found by its place among them. Over no marker, the selected location stands in.
  function mapHit(doc, isShown) {
    var state = vueSetup(doc);
    var list = state && state.mapList ? Array.prototype.slice.call(state.mapList).filter(isShown) : [];
    var marker = list[doc.__fgMarker];
    var active = list.filter(function (entry) { return entry.isActive; })[0];
    var entry = marker || active;

    return entry && entry.configId ? { kind: 'place', id: entry.configId } : null;
  }

  function watchMarkers(doc) {
    if (doc.__fgWatched) return;

    doc.__fgWatched = true;
    doc.__fgMarker = -1;
    doc.addEventListener('mouseover', function (e) {
      var marker = e.target && e.target.closest ? e.target.closest('.map-marker') : null;

      doc.__fgMarker = marker ? Array.prototype.indexOf.call(doc.querySelectorAll('.map-marker'), marker) : -1;
    });
  }

  // The config id of every item instance the game has sent a page, by instance id. Instance ids are unique, so one map
  // serves every page; it is emptied when it grows large, since a page resends its items whenever they change.
  var itemConfigs = {};
  var itemConfigCount = 0;
  var MAX_ITEM_CONFIGS = 20000;
  var isHearingGame = false;

  function hearGame() {
    if (isHearingGame || !window.vuplex || !window.vuplex.addEventListener) return;

    isHearingGame = true;
    window.vuplex.addEventListener('message', function (e) {
      try {
        learnItems(e.data);
      } catch (err) {}
    });
  }

  // Root.html's protocol: type 8 hands a page a message, fields split by the record separator, JSON last.
  function learnItems(message) {
    if (typeof message !== 'string' || message.indexOf('8\u001e') !== 0) return;
    if (message.indexOf('"configId"') < 0 || message.indexOf('"itemId"') < 0) return;

    var json = message.split('\u001e').slice(3).join('\u001e');

    collectItems(JSON.parse(json));
  }

  function collectItems(node) {
    if (!node || typeof node !== 'object') return;

    if (node.itemId != null && node.configId) rememberItem(String(node.itemId), node.configId);

    for (var key in node) {
      if (Object.prototype.hasOwnProperty.call(node, key)) collectItems(node[key]);
    }
  }

  function rememberItem(itemId, configId) {
    if (itemConfigCount >= MAX_ITEM_CONFIGS) {
      itemConfigs = {};
      itemConfigCount = 0;
    }

    if (!(itemId in itemConfigs)) itemConfigCount++;

    itemConfigs[itemId] = configId;
  }

  function watchItems(doc) {
    if (doc.__fgItemsWatched) return;

    doc.__fgItemsWatched = true;
    doc.__fgItem = null;
    doc.addEventListener('mouseover', function (e) {
      var item = e.target && e.target.closest ? e.target.closest('.item') : null;

      doc.__fgItem = item && item.id ? item.id : null;
    });
    // Leaving the page altogether fires no mouseover inside it, so the last item would otherwise stay hovered.
    doc.addEventListener('mouseout', function (e) {
      if (!e.relatedTarget) doc.__fgItem = null;
    });
  }

  function gridItemHit(doc) {
    var configId = doc.__fgItem ? itemConfigs[doc.__fgItem] : null;

    return configId ? { kind: 'item', id: configId } : null;
  }

  var LOOKUPS = [
    { pattern: /BackpackUI\.html/, hit: backpackHit },
    { pattern: /TradeUI\.html/, hit: tradeHit },
    { pattern: /ExploreMap\.html/, hit: function (doc) { return mapHit(doc, function (entry) { return entry.lockState !== 2; }); } },
    { pattern: /PreDisasterMap\.html/, hit: function (doc) { return mapHit(doc, function () { return true; }); } },
    { pattern: /\.html/, hit: gridItemHit }
  ];

  function hovered() {
    var docs = gameDocs();

    for (var i = 0; i < LOOKUPS.length; i++) {
      for (var j = 0; j < docs.length; j++) {
        if (!LOOKUPS[i].pattern.test(docs[j].src)) continue;

        try {
          var hit = LOOKUPS[i].hit(docs[j].doc);

          if (hit) return hit;
        } catch (e) {}
      }
    }

    return null;
  }

  // ===== The game's pages =====

  // The game's pages are same-origin iframes, so their documents can be reached from here.
  function gameDocs() {
    var frames = document.querySelectorAll('iframe');
    var docs = [];

    for (var i = 0; i < frames.length; i++) {
      var src = frames[i].src || '';
      var doc = null;

      if (ui && frames[i] === ui.frame) continue;

      try {
        doc = frames[i].contentDocument;
      } catch (e) {}

      if (doc) docs.push({ src: src, doc: doc });
      else if (/\.html/.test(src) && src.indexOf('file:') === 0 && !hasReportedPageAccess) {
        hasReportedPageAccess = true;
        send('diag|cannot reach a game page document (root ' + location.protocol + ', iframe ' + src + ')');
      }
    }

    return docs;
  }

  var PAGES = [
    { pattern: /CoreUI0\.html/, patch: function (doc) { placeHudButton(doc, prepHudButton); } },
    { pattern: /CoreUI1\.html/, patch: function (doc) { placeHudButton(doc, hudButton); } },
    { pattern: /ExploreMap\.html|PreDisasterMap\.html/, patch: watchMarkers },
    { pattern: /PhoneSMS\.html/, patch: patchPhone },
    { pattern: /\.html/, patch: watchItems }
  ];

  // Polled because the game creates and drops its page iframes whenever it likes.
  function patchPages() {
    var docs = gameDocs();

    hearGame();

    var isPhoneShown = false;

    docs.forEach(function (page) {
      PAGES.forEach(function (entry) {
        if (entry.pattern.test(page.src)) entry.patch(page.doc);
      });

      if (/PhoneSMS\.html/.test(page.src) && page.doc.querySelector('.' + PHONE_VIEW_CLASS)) isPhoneShown = true;
    });

    if (phone.screen && !isPhoneShown) phoneClosed();
  }

  function patchPagesSafely() {
    try {
      patchPages();
    } catch (e) {}
  }

  // First in the tool column: the column is anchored at the bottom, so the first child sits on top and the game's own
  // buttons do not move. The column hides itself in build mode, and the button with it.
  function placeHudButton(doc, buildButton) {
    var column = doc.querySelector('.vertical-tools');
    var existing = doc.querySelector('.' + HUD_CLASS);

    if (!column) return;
    if (existing && existing.parentNode === column) return;
    if (existing) existing.remove();

    var node = buildButton(doc);

    node.setAttribute('data-etk-dock', DOCK_ORDER);
    column.insertBefore(node, column.firstChild);

    if (!hasReportedHudPlaced) send('diag|HUD button placed in the tool column');
    hasReportedHudPlaced = true;
  }

  // The same markup as CoreUI1's bag button: its classes, an svg icon, and the hover tip its stylesheet shows. Every
  // game page watches its body for new buttons and tells the game where they are, so a click on this one reaches it.
  function hudButton(doc) {
    var node = toggleButton(doc, 'ui-btn icon-sq interactive-btn has-entry-tip ' + HUD_CLASS);
    var tip = make(doc, 'div', 'entry-tip');

    tip.appendChild(make(doc, 'div', 'entry-tip-title', say('title')));
    tip.appendChild(make(doc, 'div', 'entry-tip-body', say('tip', view.hotkey)));
    node.appendChild(tip);

    return node;
  }

  // CoreUI0's tool buttons sit in a wrapper with their hover tip beside them, to the right.
  function prepHudButton(doc) {
    var wrapper = make(doc, 'div', 'tooltip-wrapper ' + HUD_CLASS);
    var tip = make(doc, 'div', 'btn-tooltip btn-tooltip--right');

    tip.appendChild(make(doc, 'div', 'tip-name', say('title')));
    tip.appendChild(make(doc, 'div', 'tip-desc', say('tip', view.hotkey)));
    wrapper.appendChild(toggleButton(doc, 'ui-btn icon-sq interactive-btn'));
    wrapper.appendChild(tip);

    return wrapper;
  }

  function toggleButton(doc, className) {
    var node = make(doc, 'button', className);
    var svg = doc.createElementNS(SVG_NS, 'svg');
    var book = doc.createElementNS(SVG_NS, 'path');

    svg.setAttribute('class', 'icon-svg');
    svg.setAttribute('viewBox', '0 0 24 24');
    book.setAttribute('d', BOOK_FILL);
    book.setAttribute('fill-rule', 'evenodd');
    svg.appendChild(book);
    node.setAttribute('data-interactive', '');
    node.appendChild(svg);
    node.addEventListener('click', function (e) {
      e.stopPropagation();
      send('toggle');
    });

    return node;
  }

  // ===== The phone app =====

  // The app sits on the phone's home screen beside the game's own, and opens the site in the phone's own frame, where
  // its narrow width gives the site's phone layout.
  var PHONE_CSS = [
    '.app.is-fg .app-icon{background:linear-gradient(180deg,#9fd77a,#3f7a2a)}',
    '.fg-phone{position:absolute;left:0;top:0;right:0;bottom:0;z-index:50;display:flex;flex-direction:column;background:#16130f}',
    '.fg-phone-bar{flex:none;display:flex;align-items:center;gap:10px;height:40px;padding:0 10px;background:#2a2016;color:#fbb034;font-size:15px}',
    '.fg-phone-back{background:transparent;border:1px solid rgba(251,176,52,.45);border-radius:4px;color:#fbb034;padding:3px 10px;font-size:13px;cursor:pointer}',
    '.fg-phone-title{flex:1}',
    '.fg-phone-btn{background:transparent;border:none;color:#fbb034;padding:6px;line-height:0;cursor:pointer}',
    '.fg-phone-x{font-size:20px;line-height:16px;padding:4px 6px}',
    '.fg-icon{width:14px;height:14px;fill:none;stroke:currentColor;stroke-width:1.2}',
    '.fg-phone-frame{flex:1;width:100%;border:0;background:#16130f}',
    '.fg-phone-empty{flex:1;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:12px;padding:24px;color:#c8c8c8;font-size:14px;text-align:center}',
    '.fg-phone-get{background:transparent;border:1px solid rgba(251,176,52,.45);border-radius:4px;color:#fbb034;padding:4px 10px;font-size:13px;cursor:pointer}',
    '.fg-phone-get:disabled{opacity:.6;cursor:default}'
  ].join('\n');

  function patchPhone(doc) {
    var grid = doc.querySelector('.app-grid');

    if (!grid || grid.querySelector('.' + PHONE_APP_CLASS)) return;

    addPhoneStyle(doc);
    grid.appendChild(phoneApp(doc));

    if (!hasReportedPhonePlaced) send('diag|app placed on the phone');
    hasReportedPhonePlaced = true;
  }

  function addPhoneStyle(doc) {
    if (doc.getElementById(PHONE_STYLE_ID)) return;

    var style = doc.createElement('style');

    style.id = PHONE_STYLE_ID;
    style.textContent = PHONE_CSS;
    (doc.head || doc.body).appendChild(style);
  }

  // The markup of the phone's own apps: an icon square with a stroked glyph, and the label under it.
  function phoneApp(doc) {
    var app = make(doc, 'div', 'app is-fg ' + PHONE_APP_CLASS);
    var icon = make(doc, 'div', 'app-icon');
    var glyph = make(doc, 'div', 'icon-glyph');
    var svg = doc.createElementNS(SVG_NS, 'svg');

    svg.setAttribute('viewBox', '0 0 24 24');
    svg.setAttribute('fill', 'none');
    BOOK_STROKES.forEach(function (d) {
      var stroke = doc.createElementNS(SVG_NS, 'path');

      stroke.setAttribute('d', d);
      stroke.setAttribute('stroke-linejoin', 'round');
      svg.appendChild(stroke);
    });
    glyph.appendChild(svg);
    icon.appendChild(glyph);
    app.appendChild(icon);
    app.appendChild(make(doc, 'div', 'app-label', say('title')));
    app.addEventListener('click', function (e) {
      e.stopPropagation();
      openPhone(doc);
    });

    return app;
  }

  // Tapping the app shows a minimized screen again as it was left; otherwise it starts at the site home.
  function openPhone(doc) {
    var panel = doc.querySelector('.sms-panel');
    var existing = panel ? panel.querySelector('.' + PHONE_VIEW_CLASS) : null;

    if (!panel) return;

    if (existing) {
      if (existing === phone.screen && !phone.isOpen) showPhone(true);

      return;
    }

    var screen = phoneScreen(doc);

    panel.appendChild(screen);
    phone = { isOpen: true, path: homePath(), at: null, screen: screen };
    fillPhone();
    send('phone|1');
  }

  function phoneScreen(doc) {
    var screen = make(doc, 'div', PHONE_VIEW_CLASS);
    var bar = make(doc, 'div', 'fg-phone-bar');

    bar.appendChild(phoneButton(doc, 'fg-phone-back', say('back'), closePhone));
    bar.appendChild(make(doc, 'span', 'fg-phone-title', say('title')));
    bar.appendChild(phoneIconButton(doc, 'minimize', function () { showPhone(false); }));
    bar.appendChild(phoneIconButton(doc, 'expand', expandPhone));
    bar.appendChild(phoneButton(doc, 'fg-phone-btn fg-phone-x', '\u00d7', closePhone)).title = say('close');
    screen.appendChild(bar);
    screen.addEventListener('mousedown', function (e) { e.stopPropagation(); });

    return screen;
  }

  function phoneButton(doc, className, text, act) {
    var button = make(doc, 'button', className, text);

    button.addEventListener('click', function (e) {
      e.stopPropagation();
      act();
    });

    return button;
  }

  function phoneIconButton(doc, name, act) {
    var button = phoneButton(doc, 'fg-phone-btn fg-phone-' + name, null, act);

    drawIcon(button, name);
    button.title = say(name);

    return button;
  }

  // A minimized screen stays in the phone, page and all, behind the home screen, and gives the keyboard back.
  function showPhone(isShown) {
    phone.screen.style.display = isShown ? '' : 'none';
    phone.isOpen = isShown;

    if (isShown) fillPhone();

    send('phone|' + (isShown ? 1 : 0));
  }

  function closePhone() {
    if (phone.screen) phone.screen.remove();

    phoneClosed();
  }

  // The page the phone is on moves to the large window, and the phone app closes.
  function expandPhone() {
    var to = phone.at || phone.path || homePath();

    closePhone();
    open(to);
  }

  function phoneClosed() {
    phone = { isOpen: false, path: null };
    send('phone|0');
  }

  // Draws the phone screen under its bar for the current view: the site, or the download offer when there is no site.
  function fillPhone() {
    var doc = phone.screen.ownerDocument;
    var current = phone.screen.querySelector('.fg-phone-slot');

    if (view.base && current && current.getAttribute('data-base') === view.base) return;

    var next = view.base ? phoneSite(doc) : phoneOffer(doc);

    if (current) current.parentNode.replaceChild(next, current);
    else phone.screen.appendChild(next);
  }

  function phoneSite(doc) {
    var site = make(doc, 'iframe', 'fg-phone-slot fg-phone-frame');

    site.name = PHONE_FRAME_NAME;
    site.setAttribute('data-base', view.base);
    site.src = siteAddress(phone.path, 'phone');

    return site;
  }

  function phoneOffer(doc) {
    var offer = make(doc, 'div', 'fg-phone-slot fg-phone-empty');
    var get = make(doc, 'button', 'fg-phone-get', view.action || '');

    offer.appendChild(make(doc, 'div', null, say('needCopy')));
    get.disabled = !view.canDownload;
    get.style.display = view.action ? '' : 'none';
    get.addEventListener('click', function (e) {
      e.stopPropagation();
      send('download');
    });
    offer.appendChild(get);

    return offer;
  }

  function refreshPhone() {
    if (phone.isOpen && phone.screen && phone.screen.parentNode) fillPhone();
  }

  // ===== Start =====

  setInterval(patchPagesSafely, 400);
  window.addEventListener('message', onSiteMessage);
  // The plugin forgets the window's place, and a drag, if it stops hearing about them.
  setInterval(function () {
    if (drag) send('dragging');
    else report();
  }, 1000);

  window.__slfieldguide = { setup: setup, toggle: toggle, lookup: lookup, close: close };
  send('ready');
})();
