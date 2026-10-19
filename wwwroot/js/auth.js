(function () {
    // Toggle "tampilkan password"
    document.querySelectorAll('[data-toggle-password]').forEach(function (button) {
        var input = document.getElementById(button.getAttribute('data-toggle-password'));
        if (!input) return;

        button.addEventListener('click', function () {
            var show = input.type === 'password';
            input.type = show ? 'text' : 'password';
            button.textContent = show ? 'Sembunyikan' : 'Tampilkan';
            button.setAttribute('aria-pressed', show ? 'true' : 'false');
            button.setAttribute('aria-label', show ? 'Sembunyikan password' : 'Tampilkan password');
        });
    });

    // Disable tombol submit setelah klik (cegah double-submit)
    document.querySelectorAll('form[data-auth-form]').forEach(function (form) {
        form.addEventListener('submit', function () {
            if (window.jQuery && jQuery.fn.valid && !jQuery(form).valid()) return;

            setTimeout(function () {
                form.querySelectorAll('button[type="submit"]').forEach(function (btn) {
                    btn.disabled = true;
                    btn.setAttribute('aria-busy', 'true');
                });
            }, 0);
        });
    });

    // Tombol kembali aktif lagi kalau halaman dipulihkan dari cache (tombol Back)
    window.addEventListener('pageshow', function (event) {
        if (!event.persisted) return;
        document.querySelectorAll('form[data-auth-form] button[type="submit"]').forEach(function (btn) {
            btn.disabled = false;
            btn.removeAttribute('aria-busy');
        });
    });
})();
