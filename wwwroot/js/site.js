// Scroll progress indicator
window.addEventListener("scroll", () => {
    const max = document.documentElement.scrollHeight - window.innerHeight;
    const ratio = max > 0 ? (window.scrollY / max) : 0;
    const progress = document.getElementById("scroll-progress");
    if (progress) {
        progress.style.transform = `scaleX(${Math.min(Math.max(ratio, 0), 1)})`;
    }
});

// Theme toggle
async function toggleTheme() {
    const html = document.documentElement;
    const current = html.getAttribute('data-theme') || 'light';
    const newTheme = current === 'light' ? 'dark' : 'light';

    // Update immediately
    html.setAttribute('data-theme', newTheme);
    updateThemeIcon(newTheme);

    try {
        await fetch('/api/Theme/Set', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ theme: newTheme })
        });
    } catch (e) {
        console.error('Failed to save theme preference', e);
    }
}

function updateThemeIcon(theme) {
    const icon = document.getElementById('theme-icon');
    if (icon) {
        icon.textContent = theme === 'dark' ? '☀️' : '🌙';
    }
}

document.addEventListener('DOMContentLoaded', () => {
    // Set correct icon on page load based on current data-theme
    const html = document.documentElement;
    const theme = html.getAttribute('data-theme') || 'light';
    updateThemeIcon(theme);

    // Attach click handler to theme toggle button
    const toggleBtn = document.getElementById('theme-toggle');
    if (toggleBtn) {
        toggleBtn.addEventListener('click', toggleTheme);
    }
});
