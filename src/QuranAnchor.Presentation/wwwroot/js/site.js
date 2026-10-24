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
