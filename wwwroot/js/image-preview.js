// Shows thumbnails for any <input type="file" data-preview="targetId">
document.querySelectorAll('input[type=file][data-preview]').forEach(input => {
    input.addEventListener('change', () => {
        const box = document.getElementById(input.dataset.preview);
        if (!box) return;
        box.innerHTML = '';
        [...input.files].forEach(file => {
            const img = document.createElement('img');
            img.src = URL.createObjectURL(file);
            img.className = 'rounded me-2 mb-2';
            img.style.cssText = 'width:120px;height:90px;object-fit:cover';
            box.appendChild(img);
        });
    });
});
