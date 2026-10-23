// Just enough of Root.html, the HUD page (UI/CoreUI1.html), the phone (UI/PhoneSMS.html), the backpack, the trade
// screen and the maps for page.js to run in Jint: the root document with its page iframes, the vuplex bridge, the
// pointer, and events that bubble. Test code only; nothing here ships.
var window = this;

var location = { protocol: 'file:', href: 'file:///game/WebUI/Root.html' };

var innerWidth = 1000;
var innerHeight = 500;

var intervals = [];

// Root.html's window: the site's pages post their messages to it.
var windowListeners = {};

window.addEventListener = function (type, listener) {
  (windowListeners[type] = windowListeners[type] || []).push(listener);
};

// A site page in the named frame says which page it is on, as src/designs/depot/Frame.astro does.
function siteSays(frameName, at) {
  (windowListeners.message || []).forEach(function (listener) { listener({ data: 'slfg-at|' + frameName + '|' + at }); });
}

function setInterval(callback) {
  intervals.push(callback);

  return intervals.length;
}

// One run of every interval the script set: its 400 ms page check and its one-second report.
function poll() {
  intervals.slice().forEach(function (callback) { callback(); });
}

function getComputedStyle() {
  return { cursor: 'ew-resize' };
}

var FakeElement = class {
  constructor(tagName, ownerDocument) {
    var element = this;

    this.tagName = tagName;
    this.ownerDocument = ownerDocument;
    this.id = '';
    this.parentElement = null;
    this.children = [];
    this.text = '';
    this.attrs = {};
    this.classes = [];
    this.listeners = {};
    this.style = {};
    this.classList = {
      contains: function (name) { return element.classes.indexOf(name) >= 0; },
      add: function (name) { if (element.classes.indexOf(name) < 0) element.classes.push(name); },
      remove: function (name) { element.classes = element.classes.filter(function (c) { return c !== name; }); }
    };
  }

  get parentNode() { return this.parentElement; }

  get firstChild() { return this.children.length ? this.children[0] : null; }

  get className() { return this.classes.join(' '); }

  set className(value) { this.classes = String(value).split(' ').filter(function (name) { return name !== ''; }); }

  get textContent() {
    return this.text + this.children.map(function (child) { return child.textContent; }).join('');
  }

  set textContent(value) {
    this.children.slice().forEach(function (child) { this.removeChild(child); }, this);
    this.text = value == null ? '' : String(value);
  }

  hasAttribute(name) { return Object.prototype.hasOwnProperty.call(this.attrs, name); }

  getAttribute(name) { return this.hasAttribute(name) ? this.attrs[name] : null; }

  setAttribute(name, value) { this.attrs[name] = String(value); }

  addEventListener(type, listener) {
    (this.listeners[type] = this.listeners[type] || []).push(listener);
  }

  appendChild(child) {
    if (child.parentElement) child.parentElement.removeChild(child);

    child.parentElement = this;
    this.children.push(child);

    return child;
  }

  insertBefore(child, before) {
    if (!before) return this.appendChild(child);

    if (child.parentElement) child.parentElement.removeChild(child);

    child.parentElement = this;
    this.children.splice(this.children.indexOf(before), 0, child);

    return child;
  }

  replaceChild(child, old) {
    if (child.parentElement) child.parentElement.removeChild(child);

    this.children[this.children.indexOf(old)] = child;
    child.parentElement = this;
    old.parentElement = null;

    return old;
  }

  removeChild(child) {
    this.children = this.children.filter(function (c) { return c !== child; });
    child.parentElement = null;

    return child;
  }

  remove() {
    if (this.parentElement) this.parentElement.removeChild(this);
  }

  contains(other) {
    for (var element = other; element; element = element.parentElement) {
      if (element === this) return true;
    }

    return false;
  }

  matches(selector) {
    var element = this;

    return selectorSteps(selector).every(function (step) { return matchesStep(element, step); });
  }

  closest(selector) {
    for (var element = this; element; element = element.parentElement) {
      if (element.matches(selector)) return element;
    }

    return null;
  }

  querySelectorAll(selector) {
    return descendantsOf([this]).filter(function (element) { return element.matches(selector); });
  }

  querySelector(selector) {
    return this.querySelectorAll(selector)[0] || null;
  }
};

// Compound selectors of a tag, classes, ids and attributes; the script uses nothing else.
function selectorSteps(selector) {
  var steps = selector.match(/^[a-z]+|\.[\w-]+|#[\w-]+|\[[\w-]+\]/g) || [];

  if (steps.join('') !== selector) throw new Error('the fake page does not know the selector "' + selector + '"');

  return steps;
}

function matchesStep(element, step) {
  switch (step.charAt(0)) {
    case '.':
      return element.classList.contains(step.slice(1));
    case '#':
      return element.id === step.slice(1);
    case '[':
      return element.hasAttribute(step.slice(1, -1));
    default:
      return element.tagName === step;
  }
}

function descendantsOf(roots) {
  var found = [];

  roots.forEach(function (root) {
    root.children.forEach(function walk(child) {
      found.push(child);
      child.children.forEach(walk);
    });
  });

  return found;
}

var FakeDocument = class {
  constructor() {
    this.head = new FakeElement('head', this);
    this.body = new FakeElement('body', this);
    this.listeners = {};
  }

  createElement(tagName) { return new FakeElement(tagName, this); }

  createElementNS(namespace, tagName) { return new FakeElement(tagName, this); }

  addEventListener(type, listener) {
    (this.listeners[type] = this.listeners[type] || []).push(listener);
  }

  getElementById(id) {
    return descendantsOf([this.head, this.body]).filter(function (element) { return element.id === id; })[0] || null;
  }

  querySelectorAll(selector) {
    return descendantsOf([this.head, this.body]).filter(function (element) { return element.matches(selector); });
  }

  querySelector(selector) {
    return this.querySelectorAll(selector)[0] || null;
  }
};

// Root.html's own document, which holds the page iframes.
var document = new FakeDocument();

// What the script posts to the plugin.
var posted = [];

// What the game sends the pages, which Root.html receives first.
var vuplexListeners = [];

window.vuplex = {
  postMessage: function (message) { posted.push(message); },
  addEventListener: function (type, listener) { if (type === 'message') vuplexListeners.push(listener); }
};

// Root.html's protocol: type 8 forwards to the page with that frame id, then the message type, then its JSON.
function unitySays(pageId, type, data) {
  var message = ['8', pageId, type, JSON.stringify(data)].join('\u001e');

  vuplexListeners.forEach(function (listener) { listener({ data: message }); });
}

// A workbench page's item grid (webui-bag.js): each item element is named by its instance id and holds an icon.
function workbenchPage(src, itemIds) {
  var doc = frame(src);
  var grid = doc.body.appendChild(new FakeElement('div', doc));

  grid.className = 'grid';
  itemIds.forEach(function (itemId) {
    var item = grid.appendChild(new FakeElement('div', doc));

    item.className = 'item';
    item.id = String(itemId);
    item.appendChild(new FakeElement('img', doc)).className = 'icon';
  });

  return doc;
}

function itemIcon(doc, itemId) {
  return doc.getElementById(String(itemId)).querySelector('.icon');
}

// ===== Events =====

// Dispatched on the target and bubbled up to its document until a listener stops it.
function fire(type, target, fields) {
  var isStopped = false;
  var event = {
    type: type, target: target, button: 0, clientX: 0, clientY: 0,
    stopPropagation: function () { isStopped = true; },
    preventDefault: function () {}
  };

  Object.keys(fields || {}).forEach(function (key) { event[key] = fields[key]; });

  for (var node = target; node && !isStopped; node = node.parentElement) {
    (node.listeners[type] || []).slice().forEach(function (listener) { listener(event); });
  }

  if (!isStopped && target.ownerDocument) (target.ownerDocument.listeners[type] || []).slice().forEach(function (listener) { listener(event); });
}

function click(target) {
  fire('click', target);
}

// A press at one point, the pointer moved to another, and the button let go there, as a player drags.
function dragFrom(target, fromX, fromY, toX, toY) {
  fire('mousedown', target, { clientX: fromX, clientY: fromY });
  fire('mousemove', document.getElementById('fg-shield') || target, { clientX: toX, clientY: toY });
  fire('mouseup', document.getElementById('fg-shield') || target, { clientX: toX, clientY: toY });
}

// ===== The game's pages =====

function frame(src) {
  var node = document.body.appendChild(new FakeElement('iframe', document));

  node.src = src;
  node.contentDocument = new FakeDocument();

  return node.contentDocument;
}

function pageOf(pattern) {
  var found = document.querySelectorAll('iframe').filter(function (node) { return pattern.test(node.src); })[0];

  return found ? found.contentDocument : null;
}

function dropPage(pattern) {
  document.querySelectorAll('iframe').filter(function (node) { return pattern.test(node.src); }).forEach(function (node) { node.remove(); });
}

// A Vue page: its root element carries the component's setup state, as the game's production build exposes it.
function vuePage(src, state) {
  var doc = frame(src);
  var app = doc.body.appendChild(new FakeElement('div', doc));

  app.id = 'app';
  app._vnode = { component: { setupState: state } };

  return doc;
}

function openHud() {
  var doc = frame('file:///game/WebUI/UI/CoreUI1/CoreUI1.html');
  var column = doc.body.appendChild(new FakeElement('div', doc));

  column.className = 'vertical-tools';
  column.appendChild(new FakeElement('button', doc)).className = 'ui-btn icon-sq';

  return doc;
}

function hudButton() {
  return pageOf(/CoreUI1/).querySelector('.fg-hud-btn');
}

// The phone's SMS page: its panel, and the home screen's app grid the game leaves empty.
function openPhone() {
  var doc = frame('file:///game/WebUI/UI/PhoneSMS/PhoneSMS.html');
  var panel = doc.body.appendChild(new FakeElement('div', doc));
  var stage = panel.appendChild(new FakeElement('div', doc));

  panel.className = 'sms-panel';
  stage.className = 'home-stage';
  stage.appendChild(new FakeElement('div', doc)).className = 'app-grid';

  return doc;
}

function phonePage() {
  return pageOf(/PhoneSMS/);
}

// A map page with one marker per shown location, in list order.
function openMap(name, mapList) {
  var doc = vuePage('file:///game/WebUI/UI/' + name + '/' + name + '.html', { mapList: mapList });
  var shown = name === 'ExploreMap' ? mapList.filter(function (entry) { return entry.lockState !== 2; }) : mapList;

  shown.forEach(function () {
    doc.body.appendChild(new FakeElement('div', doc)).className = 'map-marker';
  });

  return doc;
}

function marker(doc, index) {
  return doc.querySelectorAll('.map-marker')[index];
}

// ===== The window =====

function guideWindow() {
  return document.getElementById('fg-win');
}

function windowButton(act) {
  return guideWindow().querySelectorAll('[data-act]').filter(function (node) { return node.getAttribute('data-act') === act; })[0];
}

function guideFrame() {
  return guideWindow().querySelector('.fg-frame');
}
