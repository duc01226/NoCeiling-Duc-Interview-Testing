export function file_isValidExtension(
    file: File,
    allowedFileType: string,
    separator: string | RegExp = new RegExp(/, |,/)
): boolean {
    if (!allowedFileType) return false;

    const extensions = file.name.match(/\.[^.]+$/);
    if (!extensions?.length) return false;

    const acceptedFileType = allowedFileType.split(separator);
    if (!acceptedFileType.length) return false;

    return acceptedFileType.includes(extensions[extensions.length - 1]!);
}

export function file_renameDuplicateName(originalFileName: string, existingFileNames: string[]) {
    let newFileName = originalFileName;
    let counter = 1;
    const ext = originalFileName.split('.').pop();
    const baseName = originalFileName.replace(`.${ext}`, '');

    while (existingFileNames.includes(newFileName)) {
        newFileName = `${baseName.replace(`.${ext}`, '')} (${counter}).${ext}`;
        counter++;
    }

    return newFileName;
}

/**
 * Exports HTML content to a DOC file.
 * @param elementId The ID of the HTML element to export.
 * @param filename The name of the file (without extension). Defaults to 'document-name'.
 */
export function file_exportToDoc(elementId: string, filename: string = 'document-name'): void {
    const headHtml: string = `<html xmlns:o='urn:schemas-microsoft-com:office:office' xmlns:w='urn:schemas-microsoft-com:office:word' xmlns='http://www.w3.org/TR/REC-html40'>
            <head>
                <meta charset='utf-8'><title>Export HTML To Doc</title>
            </head>
        <body>`;

    const endHtml: string = '</body></html>';

    // Complete HTML content
    const html: string = headHtml + document.getElementById(elementId)!.innerHTML + endHtml;

    // Create a data URL for the file
    const url: string = 'data:application/vnd.ms-word;charset=utf-8,' + encodeURIComponent(html);

    // Complete filename with extension
    const completeFilename: string = filename ? filename + '.doc' : 'document.doc';

    // Create a download link element
    const downloadLink = document.createElement('a');
    document.body.appendChild(downloadLink);

    // For other browsers, create a link to the file
    downloadLink.href = url;

    // Set the filename
    downloadLink.download = completeFilename;

    // Trigger the download
    downloadLink.click();

    // Remove the download link element
    document.body.removeChild(downloadLink);
}

/**
 * Downloads a file from a Blob object.
 * @param data The Blob data to download.
 * @param fileName The name of the file to download.
 */
export function file_downloadFile(data: Blob, fileName: string): void {
    const blob = new Blob([data], { type: data.type });

    // Create a link element for downloading the file
    const a = document.createElement('a');
    a.href = window.URL.createObjectURL(blob);
    a.setAttribute('download', fileName);
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
}
