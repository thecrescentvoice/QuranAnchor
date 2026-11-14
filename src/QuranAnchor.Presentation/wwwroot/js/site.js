// Show or hide a password field.
document.addEventListener('click', function (event) {
    var button = event.target.closest('[data-toggle-password]');
    if (!button) { return; }

    var group = button.closest('.input-group');
    var input = group ? group.querySelector('input') : null;
    if (!input) { return; }

    var showing = input.type === 'text';
    input.type = showing ? 'password' : 'text';
    button.setAttribute('aria-label', showing ? 'Show password' : 'Hide password');

    var icon = button.querySelector('i');
    if (icon) { icon.className = showing ? 'bi bi-eye' : 'bi bi-eye-slash'; }
});

// Slide-out menu on phones.
document.addEventListener('click', function (event) {
    if (event.target.closest('[data-nav-open]')) { document.body.classList.add('nav-open'); return; }
    if (event.target.closest('[data-nav-close]') || event.target.closest('.sidebar .nav-item')) { document.body.classList.remove('nav-open'); }
});
document.addEventListener('keydown', function (event) {
    if (event.key === 'Escape') { document.body.classList.remove('nav-open'); }
});

// Flyers: clicking a flyer opens it large, with a Download button.
document.addEventListener('click', function (event) {
    var link = event.target.closest('[data-flyer-src]');
    if (!link) { return; }
    event.preventDefault();
    var modalEl = document.getElementById('flyerModal');
    if (!modalEl || !window.bootstrap) { window.open(link.getAttribute('data-flyer-src'), '_blank'); return; }
    var src = link.getAttribute('data-flyer-src');
    document.getElementById('flyerImg').src = src;
    document.getElementById('flyerImg').alt = link.getAttribute('data-flyer-title') || 'Flyer';
    document.getElementById('flyerTitle').textContent = link.getAttribute('data-flyer-title') || 'Flyer';
    var download = document.getElementById('flyerDownload');
    download.href = src;
    download.setAttribute('download', link.getAttribute('data-flyer-name') || 'flyer');
    window.bootstrap.Modal.getOrCreateInstance(modalEl).show();
});
