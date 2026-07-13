function copyToClipboard(btn) {
  var header = btn.closest('.file-preview-header');
  var wrap = btn.closest('.code-block-wrap');
  var codeBlock = header ? header.nextElementSibling : wrap ? wrap.querySelector('.code-block') : null;
  if (!codeBlock) return;
  var text = codeBlock.textContent;
  navigator.clipboard.writeText(text).then(function() {
    var orig = btn.innerHTML;
    btn.classList.add('copied');
    btn.innerHTML = '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="20 6 9 17 4 12"/></svg> Copied!';
    setTimeout(function() {
      btn.classList.remove('copied');
      btn.innerHTML = orig;
    }, 2000);
  });
}
