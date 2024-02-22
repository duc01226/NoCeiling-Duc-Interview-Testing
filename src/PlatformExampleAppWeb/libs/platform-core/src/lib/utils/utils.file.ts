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
