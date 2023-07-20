export function dom_downloadFile(data: Blob, fileName: string) {
    const blob = new Blob([data], { type: data.type });

    const a = document.createElement('a');
    a.href = window.URL.createObjectURL(blob);
    a.setAttribute('download', fileName);
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
}
