mergeInto(LibraryManager.library, {
  UICap_HasFlag: function () {
    try {
      var s = (window.location.search || '') + (window.location.hash || '') + (window.location.href || '');
      return (s.toLowerCase().indexOf('uicapture=1') >= 0) ? 1 : 0;
    } catch (e) { return 0; }
  }
});
