/**
 * Layout JavaScript Functions
 * Handles theme toggle, sidebar, and responsive behavior
 */

// ===== THEME TOGGLE FUNCTIONALITY =====
function toggleTheme() {
    const currentTheme = document.documentElement.getAttribute('data-theme');
    const newTheme = currentTheme === 'dark' ? 'light' : 'dark';

    document.documentElement.setAttribute('data-theme', newTheme);
    localStorage.setItem('theme', newTheme);
}

// ===== SIDEBAR FUNCTIONALITY =====
function toggleSidebar() {
    const sidebar = document.getElementById('sidebar');
    sidebar.classList.toggle('show');
}

// ===== INITIALIZATION =====
document.addEventListener('DOMContentLoaded', function () {
    // Load saved theme on page load
    const savedTheme = localStorage.getItem('theme') || 'light';
    document.documentElement.setAttribute('data-theme', savedTheme);

    // Active menu highlighting (commented out but available)
    // const currentPath = window.location.pathname;
    // const menuLinks = document.querySelectorAll('.menu-link');
    // menuLinks.forEach(link => {
    //     link.classList.remove('active');
    //     if (link.getAttribute('href') === currentPath) {
    //         link.classList.add('active');
    //     }
    // });
});

// ===== MOBILE RESPONSIVE HANDLERS =====
// Close sidebar when clicking outside on mobile
document.addEventListener('click', function (event) {
    const sidebar = document.getElementById('sidebar');
    const mobileMenuBtn = document.querySelector('.mobile-menu-btn');

    if (window.innerWidth <= 768) {
        if (!sidebar.contains(event.target) && !mobileMenuBtn.contains(event.target)) {
            sidebar.classList.remove('show');
        }
    }
});

// Handle window resize
window.addEventListener('resize', function () {
    const sidebar = document.getElementById('sidebar');
    if (window.innerWidth > 768) {
        sidebar.classList.remove('show');
    }
});

// ===== UTILITY FUNCTIONS =====
/**
 * Get current theme
 * @returns {string} 'light' or 'dark'
 */
function getCurrentTheme() {
    return document.documentElement.getAttribute('data-theme') || 'light';
}

/**
 * Set theme programmatically
 * @param {string} theme - 'light' or 'dark'
 */
function setTheme(theme) {
    if (theme === 'light' || theme === 'dark') {
        document.documentElement.setAttribute('data-theme', theme);
        localStorage.setItem('theme', theme);
    }
}

/**
 * Check if sidebar is open (mobile)
 * @returns {boolean}
 */
function isSidebarOpen() {
    const sidebar = document.getElementById('sidebar');
    return sidebar.classList.contains('show');
}

/**
 * Close sidebar (mobile)
 */
function closeSidebar() {
    const sidebar = document.getElementById('sidebar');
    sidebar.classList.remove('show');
}

/**
 * Open sidebar (mobile)
 */
function openSidebar() {
    const sidebar = document.getElementById('sidebar');
    sidebar.classList.add('show');
}