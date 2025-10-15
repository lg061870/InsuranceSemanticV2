window.setupDragAndDrop = (dotNetReference) => {
    const dropzone = document.querySelector('[data-dropzone]');
    if (!dropzone) return;

    const preventDefaults = (e) => {
        e.preventDefault();
        e.stopPropagation();
    };

    ['dragenter', 'dragover', 'dragleave', 'drop'].forEach(eventName => {
        dropzone.addEventListener(eventName, preventDefaults, false);
    });

    ['dragenter', 'dragover'].forEach(eventName => {
        dropzone.addEventListener(eventName, () => {
            dotNetReference.invokeMethodAsync('HandleDragEnter');
        }, false);
    });

    ['dragleave', 'drop'].forEach(eventName => {
        dropzone.addEventListener(eventName, () => {
            dotNetReference.invokeMethodAsync('HandleDragLeave');
        }, false);
    });

    dropzone.addEventListener('drop', async (e) => {
        const files = Array.from(e.dataTransfer.files);

        // Convert files to base64 for transfer to C# - using FileReader for efficiency
        const filesWithData = await Promise.all(files.map(async file => {
            return new Promise((resolve, reject) => {
                const reader = new FileReader();
                reader.onload = () => {
                    // FileReader returns data URL like "data:text/plain;base64,SGVsbG8gV29ybGQ="
                    // We need to extract just the base64 part after the comma
                    const base64 = reader.result.split(',')[1];
                    resolve({
                        name: file.name,
                        size: file.size,
                        type: file.type,
                        lastModified: file.lastModified,
                        data: base64
                    });
                };
                reader.onerror = reject;
                reader.readAsDataURL(file);
            });
        }));

        await dotNetReference.invokeMethodAsync('HandleDroppedFiles', filesWithData);
    }, false);
};

window.triggerFileInput = () => {
    const fileInput = document.getElementById('file-input');
    if (fileInput) fileInput.click();
};