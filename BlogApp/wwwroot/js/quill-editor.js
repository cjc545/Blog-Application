let quill;

document.addEventListener("DOMContentLoaded", function () {

    quill = new Quill('#editor', {
        theme: 'snow',
        modules: {
            toolbar: {
                container: [
                    [{ header: [1, 2, false] }],
                    ['bold', 'italic', 'underline'],
                    [{ list: 'ordered' }, { list: 'bullet' }],
                    ['link', 'image'],
                    ['clean']
                ],
                handlers: {
                    image: imageHandler
                }
            }
        }
    });

    // Preload existing content if editing
    const contentInput = document.getElementById("editor");
    if (contentInput.value) {
        quill.root.innerHTML = contentInput.value;
    }

    // Attach submit handler
    const form = document.querySelector("form");
    form.addEventListener("submit", function (e) {
        // Copy Quill HTML into hidden input
        contentInput.value = quill.root.innerHTML;

        // Optional: prevent empty posts
        if (quill.getText().trim().length === 0) {
            e.preventDefault();
            alert("Post content cannot be empty");
        }
    });

});

function imageHandler() {

    const input = document.createElement('input');
    input.type = 'file';
    input.accept = 'image/*';
    input.click();

    input.onchange = async () => {

        const file = input.files[0];

        const formData = new FormData();
        formData.append("image", file);

        const response = await fetch('/Image/Upload', {
            method: 'POST',
            body: formData
        });

        if (!response.ok) {
            console.error("Upload failed");
            return;
        }

        const imageUrl = await response.text();

        const range = quill.getSelection();
        quill.insertEmbed(range.index, 'image', imageUrl);

    };
}