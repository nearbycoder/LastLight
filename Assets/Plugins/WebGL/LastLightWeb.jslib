// Browser glue for the WebGL build (Assets/Scripts/Core/Platform.cs).
mergeInto(LibraryManager.library, {
  // Settings > Sound > Mono. Web Audio has no OnAudioFilterRead, so the fold happens at the page's output: a destination
  // of one channel mixes every source down to the middle ("speakers" down-mix), and the browser plays that one channel
  // on every speaker.
  LastLight_SetMono__deps: ['$WEBAudio'],
  LastLight_SetMono: function (on) {
    var ctx = WEBAudio.audioContext;
    window.lastLightMono = !!on;
    if (!ctx || !ctx.destination) return;
    var dest = ctx.destination;
    try {
      dest.channelInterpretation = 'speakers';
      dest.channelCountMode = 'explicit';
      dest.channelCount = on ? 1 : Math.min(2, dest.maxChannelCount || 2);
    } catch (e) {
      console.warn('[Sound] could not set the output to ' + (on ? 'mono' : 'stereo') + ': ' + e);
    }
  },

  // Music and the sea's sound are played through <audio> elements. Unity starts them when its audio context starts
  // running, which is after the tap or click that resumed it, not during it; a browser that wants every play() made
  // inside a gesture (Safari on iOS, Chromium on its strict policy) then refuses, and Unity never asks again, so the
  // title stayed silent. So on every tap, click or key, inside that event: the context is resumed, Unity's spare
  // elements are unlocked by a play() made there, and any sound the game is playing whose element sits paused (the game
  // stops sounds but never pauses them, so that was a refusal) is played again.
  LastLight_AudioUnlock__deps: ['$WEBAudio'],
  LastLight_AudioUnlock: function () {
    if (window.lastLightAudioUnlock) return;
    var unlock = function () {
      try {
        var ctx = WEBAudio.audioContext;
        if (ctx && ctx.state === 'suspended') ctx.resume().catch(function () { });
        var cache = WEBAudio.audioCache || [];
        for (var i = 0; i < cache.length; i++) {
          var spare = cache[i];
          if (spare.__lastLightUnlocked) continue;
          spare.__lastLightUnlocked = true;
          var p = spare.play();
          if (p && p.catch) p.catch(function () { });
          spare.pause();
        }
        var all = WEBAudio.audioInstances || {};
        for (var id in all) {
          var src = all[id] && all[id].source;
          var el = src && src.mediaElement;
          if (!el || src.isStopped || src.playPromise || src.playTimeout || src.pauseRequested) continue;
          if (!el.paused || el.ended || !el.getAttribute('src')) continue;
          var again = el.play();
          if (again && again.catch) again.catch(function () { });
        }
      } catch (e) { }
    };
    window.lastLightAudioUnlock = unlock;
    ['pointerup', 'touchend', 'click', 'keydown'].forEach(function (t) { window.addEventListener(t, unlock, true); });
  },

  LastLight_IsFullscreen: function () {
    return document.fullscreenElement || document.webkitFullscreenElement ? 1 : 0;
  },

  // The same call as the page's own fullscreen (unityInstance.SetFullscreen); browsers allow it for a few seconds after a
  // click or key press, so a menu choice that acts a frame after the press still qualifies.
  LastLight_RequestFullscreen: function (on) {
    if (Module.SetFullscreen) Module.SetFullscreen(on ? 1 : 0);
  },

  LastLight_WhenHidden: function (objectName, methodName) {
    var target = UTF8ToString(objectName), method = UTF8ToString(methodName);
    var send = function () { try { SendMessage(target, method); } catch (e) { } };
    document.addEventListener('visibilitychange', function () { if (document.visibilityState === 'hidden') send(); });
    window.addEventListener('pagehide', send);
  },

  // Touch: the page (index.html) keeps window.lastLightTouch: whether its on-screen controls are on (a touch-first device,
  // or a finger since the last mouse, key or pad), its buttons' state, and the room they take. The game reads it once a
  // frame and tells the page what the buttons should show.
  //   0 touch mode, 1 Focus held, 2 Focus presses, 3 Horn presses, 4 Pause/Back presses, 5 a touch-first device,
  //   6 the last visit stopped while the game was showing (most often a phone's browser out of memory)
  LastLight_TouchRead: function (which) {
    var t = window.lastLightTouch;
    if (!t) return 0;
    switch (which) {
      case 0: return t.mode ? 1 : 0;
      case 1: return t.focusHeld ? 1 : 0;
      case 2: return t.focusPresses | 0;
      case 3: return t.hornPresses | 0;
      case 4: return t.backPresses | 0;
      case 5: return t.device ? 1 : 0;
      case 6: return t.lastVisitStopped ? 1 : 0;
    }
    return 0;
  },

  // The room the buttons take, as fractions of the game's canvas: 0 the Pause button's (from the left, at the top),
  // 1 the Focus and Horn column's (from the right, at the bottom), 2 the home indicator's (from the bottom).
  LastLight_TouchLayout: function (which) {
    var t = window.lastLightTouch;
    return t && t.layout ? +t.layout[which] || 0 : 0;
  },

  // What the buttons show: the night is being played (Focus, Horn and Pause), the foghorn is tonight's and how ready it
  // is (0..1), the beam is focused, what the corner button does (0 nothing, 1 pause, 2 back), and the light's bearing in
  // degrees (for the page's checks).
  LastLight_TouchState: function (playing, horn, hornReady, focused, corner, bearing) {
    var t = window.lastLightTouch;
    if (t && t.state) t.state(playing, horn, hornReady, focused, corner, bearing);
  },

  // The dawn chart's replay as it stands (for the page's checks): where it is and how long the night ran, in seconds, and
  // whether it's playing.
  LastLight_ChartState: function (time, duration, playing) {
    window.lastLightChart = { time: time, duration: duration, playing: !!playing, at: Date.now() };
  },

  // A gamepad was used: the on-screen controls step aside, as for a mouse or a key.
  LastLight_PadUsed: function () {
    var t = window.lastLightTouch;
    if (t && t.padUsed) t.padUsed();
  },

  // The page (index.html) and Tools/check-pages.mjs wait for this: the game is up and the title showing.
  LastLight_Ready: function () {
    window.lastLightReady = true;
    if (window.lastLightOnReady) window.lastLightOnReady();
  },
});
