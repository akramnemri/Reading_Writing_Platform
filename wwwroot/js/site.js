// Scroll progress indicator
window.addEventListener("scroll", () => {
    const max = document.documentElement.scrollHeight - window.innerHeight;
    const ratio = max > 0 ? (window.scrollY / max) : 0;
    const progress = document.getElementById("scroll-progress");
    if (progress) {
        progress.style.transform = `scaleX(${Math.min(Math.max(ratio, 0), 1)})`;
    }
});
