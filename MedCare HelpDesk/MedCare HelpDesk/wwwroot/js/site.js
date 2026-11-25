/* MedCare HelpDesk - Clinical Client Enhancements */

function togglePassword(inputId, iconId) {
    const input = document.getElementById(inputId);
    const icon = document.getElementById(iconId);
    if (input && icon) {
        if (input.type === "password") {
            input.type = "text";
            icon.classList.remove("bi-eye");
            icon.classList.add("bi-eye-slash");
        } else {
            input.type = "password";
            icon.classList.remove("bi-eye-slash");
            icon.classList.add("bi-eye");
        }
    }
}

document.addEventListener("DOMContentLoaded", function () {
    // Bootstrap Toasts auto-show
    var toastElList = [].slice.call(document.querySelectorAll('.toast'));
    var toastList = toastElList.map(function (toastEl) {
        return new bootstrap.Toast(toastEl, { delay: 3800 });
    });
    toastList.forEach(toast => toast.show());

    // Mobile Sidebar Drawer Toggle
    const sidebar = document.getElementById("mcSidebar");
    const backdrop = document.getElementById("mcSidebarBackdrop");
    const sidebarToggle = document.getElementById("mcSidebarToggle");
    if (sidebar && sidebarToggle) {
        sidebarToggle.addEventListener("click", function () {
            sidebar.classList.toggle("mc-open");
            if (backdrop) backdrop.classList.toggle("show");
        });
        if (backdrop) {
            backdrop.addEventListener("click", function () {
                sidebar.classList.remove("mc-open");
                backdrop.classList.remove("show");
            });
        }
    }

    // Live Hospital UTC Clock
    const clockEl = document.getElementById("liveUtcClock");
    if (clockEl) {
        const updateClock = () => {
            const now = new Date();
            clockEl.textContent = now.toISOString().substring(11, 16) + " UTC";
        };
        updateClock();
        setInterval(updateClock, 30000);
    }

    // Back to Top Button
    const backToTopBtn = document.getElementById("btnBackToTop");
    if (backToTopBtn) {
        window.addEventListener("scroll", function () {
            if (window.scrollY > 280) {
                backToTopBtn.classList.add("show");
            } else {
                backToTopBtn.classList.remove("show");
            }
        });

        backToTopBtn.addEventListener("click", function () {
            window.scrollTo({ top: 0, behavior: "smooth" });
        });
    }

    // Night-Shift Adaptive Dark / Light Mode Toggle
    const toggleBtn = document.getElementById('darkModeToggle');
    const themeIcon = document.getElementById('themeIcon');
    const currentTheme = localStorage.getItem('medcare-theme') || 'light';

    document.documentElement.setAttribute('data-bs-theme', currentTheme);
    updateIcon(currentTheme);

    if (toggleBtn) {
        toggleBtn.addEventListener('click', function () {
            const current = document.documentElement.getAttribute('data-bs-theme');
            const newTheme = current === 'dark' ? 'light' : 'dark';
            document.documentElement.setAttribute('data-bs-theme', newTheme);
            localStorage.setItem('medcare-theme', newTheme);
            updateIcon(newTheme);
        });
    }

    function updateIcon(theme) {
        if (!themeIcon) return;
        if (theme === 'dark') {
            themeIcon.classList.remove('bi-moon-fill');
            themeIcon.classList.add('bi-sun-fill', 'text-warning');
        } else {
            themeIcon.classList.remove('bi-sun-fill', 'text-warning');
            themeIcon.classList.add('bi-moon-fill');
        }
    }
});

function updateHiddenPhone() {
    var codeEl = document.getElementById('countryCode');
    var digitsEl = document.getElementById('phoneDigits');
    var fullEl = document.getElementById('fullPhoneNumber');
    if (!codeEl || !digitsEl || !fullEl) return;
    let digits = digitsEl.value.trim();
    fullEl.value = digits.length > 0 ? (codeEl.value + ' ' + digits) : '';
}

function fillDemoCredentials(email, password) {
    const e = document.getElementById('Email');
    const p = document.getElementById('password');
    if (e && p) {
        e.value = email;
        p.value = password;
    }
}
