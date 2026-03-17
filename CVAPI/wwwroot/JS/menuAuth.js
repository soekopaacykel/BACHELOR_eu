function initializeMenuAuth() {
    // Only handle menu navigation, no auth logic
    document.querySelectorAll('.consultant-menu-bar a, .applicant-menu-bar a, .applicants-menu-bar a').forEach(link => {
        link.addEventListener('click', function(e) {
            e.preventDefault();
            const href = this.getAttribute('href');
            window.location.href = href;
        });
    });
}

document.addEventListener('DOMContentLoaded', initializeMenuAuth);
