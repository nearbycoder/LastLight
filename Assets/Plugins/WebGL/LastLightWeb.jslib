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

  // The page (index.html) and Tools/check-pages.mjs wait for this: the game is up and the title showing.
  LastLight_Ready: function () {
    window.lastLightReady = true;
    if (window.lastLightOnReady) window.lastLightOnReady();
  },
});
