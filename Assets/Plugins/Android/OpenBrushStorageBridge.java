package foundation.icosa.openbrush.storage;

import android.app.Activity;
import android.content.ContentResolver;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.database.Cursor;
import android.net.Uri;
import android.os.ParcelFileDescriptor;
import android.provider.DocumentsContract;

import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.HashSet;
import java.util.Map;
import java.util.Set;
import java.util.concurrent.atomic.AtomicInteger;

public class OpenBrushStorageBridge {
    private static final String PREFS_NAME = "OpenBrushStorage";
    private static final String OPEN_BRUSH_FOLDER_URI = "openBrushFolderUri";
    private static final String OPEN_BRUSH_FOLDER_NAME = "Open Brush";
    private static final AtomicInteger NEXT_TRANSFER_JOB_ID = new AtomicInteger(1);
    private static final AtomicInteger NEXT_TEMP_FILE_ID = new AtomicInteger(1);
    private static final Map<Integer, TransferJob> TRANSFER_JOBS = new HashMap<>();

    private static class TransferJob {
        volatile boolean done;
        volatile boolean success;
        volatile long bytesDone;
        volatile long bytesTotal;
        volatile String error = "";
    }

    private static class DocumentLookupResult {
        final Uri uri;
        final String error;

        DocumentLookupResult(Uri uri, String error) {
            this.uri = uri;
            this.error = error;
        }
    }

    public static final class DescriptorOpenResult {
        public final int fd;
        public final String documentUri;
        public final String error;

        DescriptorOpenResult(int fd, Uri documentUri, String error) {
            this.fd = fd;
            this.documentUri = documentUri == null ? "" : documentUri.toString();
            this.error = error == null ? "" : error;
        }
    }

    public static final class DirectoryQueryResult {
        public final int code;
        public final String error;
        public final String[] documentUris;
        public final String[] parentDocumentUris;
        public final String[] displayNames;
        public final String[] mimeTypes;
        public final boolean[] directories;
        public final long[] sizes;
        public final boolean[] hasSizes;
        public final long[] lastModified;
        public final boolean[] hasLastModified;
        public final long[] flags;
        public final String[] relativeDisplayPaths;

        DirectoryQueryResult(int code, String error, ArrayList<DocumentRow> rows) {
            this.code = code;
            this.error = error == null ? "" : error;
            int count = rows == null ? 0 : rows.size();
            documentUris = new String[count];
            parentDocumentUris = new String[count];
            displayNames = new String[count];
            mimeTypes = new String[count];
            directories = new boolean[count];
            sizes = new long[count];
            hasSizes = new boolean[count];
            lastModified = new long[count];
            hasLastModified = new boolean[count];
            flags = new long[count];
            relativeDisplayPaths = new String[count];
            for (int i = 0; i < count; ++i) {
                DocumentRow row = rows.get(i);
                documentUris[i] = row.documentUri;
                parentDocumentUris[i] = row.parentDocumentUri;
                displayNames[i] = row.displayName;
                mimeTypes[i] = row.mimeType;
                directories[i] = row.directory;
                sizes[i] = row.size;
                hasSizes[i] = row.hasSize;
                lastModified[i] = row.lastModified;
                hasLastModified[i] = row.hasLastModified;
                flags[i] = row.flags;
                relativeDisplayPaths[i] = row.relativeDisplayPath;
            }
        }
    }

    private static final class DocumentRow {
        String documentUri;
        String parentDocumentUri;
        String displayName;
        String mimeType;
        boolean directory;
        long size;
        boolean hasSize;
        long lastModified;
        boolean hasLastModified;
        long flags;
        String relativeDisplayPath;
    }

    public static void requestOpenBrushFolder(Activity activity) {
        Intent intent = new Intent(activity, OpenBrushStorageActivity.class);
        activity.startActivity(intent);
    }

    public static boolean hasOpenBrushFolder(Context context) {
        String uriString = getOpenBrushFolderUri(context);
        if (uriString == null || uriString.length() == 0) {
            return false;
        }

        Uri storedUri = Uri.parse(uriString);
        boolean hasPersistedGrant = false;
        for (android.content.UriPermission permission
                : context.getContentResolver().getPersistedUriPermissions()) {
            if (permission.getUri().equals(storedUri)
                    && permission.isReadPermission()
                    && permission.isWritePermission()) {
                hasPersistedGrant = true;
                break;
            }
        }

        if (hasPersistedGrant
                && canQueryRoot(context)
                && OPEN_BRUSH_FOLDER_NAME.equals(getOpenBrushFolderDisplayName(context))) {
            return true;
        }

        clearOpenBrushFolder(context);
        return false;
    }

    public static String getOpenBrushFolderDisplayName(Context context) {
        Uri root = getRootDocumentUri(context);
        if (root == null) {
            return "";
        }

        try (Cursor cursor = context.getContentResolver().query(
                root,
                new String[]{DocumentsContract.Document.COLUMN_DISPLAY_NAME},
                null,
                null,
                null)) {
            if (cursor != null && cursor.moveToFirst()) {
                String displayName = cursor.getString(0);
                return displayName != null ? displayName : "";
            }
        }
        return "";
    }

    public static void clearOpenBrushFolder(Context context) {
        SharedPreferences prefs = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
        prefs.edit().remove(OPEN_BRUSH_FOLDER_URI).apply();
    }

    public static boolean ensureDirectory(Context context, String relativePath) {
        return ensureDirectoryUri(context, relativePath) != null;
    }

    public static DirectoryQueryResult queryDirectory(Context context, String relativePath) {
        final int success = 0;
        final int notFound = 1;
        final int notReady = 2;
        final int permissionDenied = 3;
        final int providerUnavailable = 5;
        final int invalidPath = 6;
        final int failed = 7;

        String normalized = normalize(relativePath);
        if (!isSafeRelativePath(normalized)) {
            return new DirectoryQueryResult(
                    invalidPath, "Invalid shared-storage path", null);
        }

        Uri treeUri = getTreeUri(context);
        if (treeUri == null) {
            return new DirectoryQueryResult(
                    notReady, "Open Brush folder is unavailable", null);
        }

        DocumentLookupResult lookup = findDocumentUriResult(context, normalized);
        if (lookup.error != null) {
            int code = lookup.error.toLowerCase().contains("permission")
                    ? permissionDenied
                    : providerUnavailable;
            return new DirectoryQueryResult(code, lookup.error, null);
        }
        if (lookup.uri == null) {
            return new DirectoryQueryResult(
                    notFound, "Shared-storage directory does not exist", null);
        }

        ContentResolver resolver = context.getContentResolver();
        Uri childrenUri = DocumentsContract.buildChildDocumentsUriUsingTree(
                treeUri, DocumentsContract.getDocumentId(lookup.uri));
        ArrayList<DocumentRow> rows = new ArrayList<>();
        String[] projection = new String[]{
                DocumentsContract.Document.COLUMN_DOCUMENT_ID,
                DocumentsContract.Document.COLUMN_DISPLAY_NAME,
                DocumentsContract.Document.COLUMN_MIME_TYPE,
                DocumentsContract.Document.COLUMN_SIZE,
                DocumentsContract.Document.COLUMN_LAST_MODIFIED,
                DocumentsContract.Document.COLUMN_FLAGS
        };

        try (Cursor cursor = resolver.query(childrenUri, projection, null, null, null)) {
            if (cursor == null) {
                return new DirectoryQueryResult(
                        providerUnavailable, "Shared-storage query returned no result", null);
            }
            while (cursor.moveToNext()) {
                DocumentRow row = new DocumentRow();
                String documentId = cursor.getString(0);
                row.documentUri = DocumentsContract.buildDocumentUriUsingTree(
                        treeUri, documentId).toString();
                row.parentDocumentUri = lookup.uri.toString();
                row.displayName = emptyIfNull(cursor.getString(1));
                row.mimeType = emptyIfNull(cursor.getString(2));
                row.directory = DocumentsContract.Document.MIME_TYPE_DIR.equals(row.mimeType);
                row.hasSize = !cursor.isNull(3);
                row.size = row.hasSize ? cursor.getLong(3) : 0;
                row.hasLastModified = !cursor.isNull(4);
                row.lastModified = row.hasLastModified ? cursor.getLong(4) : 0;
                row.flags = cursor.isNull(5) ? 0 : cursor.getLong(5);
                row.relativeDisplayPath = normalized.length() == 0
                        ? row.displayName
                        : normalized + "/" + row.displayName;
                rows.add(row);
            }
            return new DirectoryQueryResult(success, null, rows);
        } catch (SecurityException e) {
            return new DirectoryQueryResult(permissionDenied, formatProviderError(
                    "Permission denied while querying shared storage", e), null);
        } catch (Exception e) {
            return new DirectoryQueryResult(failed, formatProviderError(
                    "Failed to query shared storage", e), null);
        }
    }

    public static DescriptorOpenResult openFileDescriptor(
            Context context, String relativePath, String mode) {
        if (!isSupportedDescriptorMode(mode)) {
            return new DescriptorOpenResult(-1, null, "Unsupported file descriptor mode");
        }

        DocumentLookupResult lookup = findDocumentUriResult(
                context, normalize(relativePath));
        if (lookup.error != null) {
            return new DescriptorOpenResult(-1, null, lookup.error);
        }
        if (lookup.uri == null) {
            return new DescriptorOpenResult(-1, null, "Shared document does not exist");
        }

        return detachFileDescriptor(context, lookup.uri, mode);
    }

    public static DescriptorOpenResult createTemporaryFileDescriptor(
            Context context, String relativeDirectory, String targetFileName, String mimeType) {
        String normalizedDirectory = normalize(relativeDirectory);
        if (!isSafeRelativePath(normalizedDirectory)
                || targetFileName == null
                || targetFileName.length() == 0
                || targetFileName.contains("/")
                || targetFileName.contains("\\")) {
            return new DescriptorOpenResult(-1, null, "Invalid temporary document path");
        }

        Uri parent = ensureDirectoryUri(context, normalizedDirectory);
        if (parent == null) {
            return new DescriptorOpenResult(-1, null, "Failed to open temporary document directory");
        }

        String temporaryName = "." + targetFileName + ".openbrush-fd-"
                + NEXT_TEMP_FILE_ID.getAndIncrement() + ".tmp";
        Uri temporary;
        try {
            temporary = DocumentsContract.createDocument(
                    context.getContentResolver(),
                    parent,
                    mimeType == null || mimeType.length() == 0
                            ? "application/octet-stream"
                            : mimeType,
                    temporaryName);
        } catch (Exception e) {
            return new DescriptorOpenResult(-1, null, formatProviderError(
                    "Failed to create temporary document", e));
        }
        if (temporary == null) {
            return new DescriptorOpenResult(
                    -1, null, "Provider returned no temporary document");
        }

        DescriptorOpenResult result = detachFileDescriptor(context, temporary, "rwt");
        if (result.fd < 0) {
            deleteDocumentQuietly(context.getContentResolver(), temporary);
        }
        return result;
    }

    public static boolean deleteDocumentUri(Context context, String documentUri) {
        if (documentUri == null || documentUri.length() == 0) {
            return false;
        }
        try {
            return DocumentsContract.deleteDocument(
                    context.getContentResolver(), Uri.parse(documentUri));
        } catch (Exception e) {
            return false;
        }
    }

    public static boolean writeFileFromPath(
            Context context, String relativePath, String sourcePath, String mimeType) {
        return writeFileFromPath(context, relativePath, sourcePath, mimeType, null);
    }

    public static int startWriteFileFromPath(
            Context context, String relativePath, String sourcePath, String mimeType) {
        TransferJob job = createTransferJob();
        int jobId = registerTransferJob(job);
        new Thread(new Runnable() {
            @Override
            public void run() {
                job.bytesTotal = getFileLength(sourcePath);
                job.success = writeFileFromPath(context, relativePath, sourcePath, mimeType, job);
                if (!job.success && job.error.length() == 0) {
                    job.error = "Failed to write " + relativePath;
                }
                job.done = true;
            }
        }, "OpenBrushSafWrite").start();
        return jobId;
    }

    public static int startCopyDirectoryFromPath(
            Context context, String relativeDestinationPath, String sourceDirectoryPath) {
        TransferJob job = createTransferJob();
        int jobId = registerTransferJob(job);
        new Thread(new Runnable() {
            @Override
            public void run() {
                File source = new File(sourceDirectoryPath);
                job.bytesTotal = countBytes(source);
                job.success = copyDirectoryFromPath(context, relativeDestinationPath, sourceDirectoryPath, job);
                if (!job.success && job.error.length() == 0) {
                    job.error = "Failed to copy " + relativeDestinationPath;
                }
                job.done = true;
            }
        }, "OpenBrushSafCopy").start();
        return jobId;
    }

    public static int startCopyDirectoryToPath(
            Context context, String relativePath, String destinationDirectoryPath,
            String[] preservedPaths) {
        TransferJob job = createTransferJob();
        int jobId = registerTransferJob(job);
        new Thread(new Runnable() {
            @Override
            public void run() {
                Set<String> preserved = new HashSet<>();
                if (preservedPaths != null) {
                    for (String path : preservedPaths) {
                        preserved.add(new File(path).getAbsolutePath());
                    }
                }
                job.success = copyDirectoryToPath(
                        context, relativePath, destinationDirectoryPath, job, preserved);
                if (!job.success && job.error.length() == 0) {
                    job.error = "Failed to copy " + relativePath + " to local cache";
                }
                job.done = true;
            }
        }, "OpenBrushSafRead").start();
        return jobId;
    }
    public static boolean isTransferJobDone(int jobId) {
        TransferJob job = getTransferJob(jobId);
        return job == null || job.done;
    }

    public static boolean didTransferJobSucceed(int jobId) {
        TransferJob job = getTransferJob(jobId);
        return job != null && job.done && job.success;
    }

    public static long getTransferJobBytesDone(int jobId) {
        TransferJob job = getTransferJob(jobId);
        return job != null ? job.bytesDone : 0;
    }

    public static long getTransferJobBytesTotal(int jobId) {
        TransferJob job = getTransferJob(jobId);
        return job != null ? job.bytesTotal : 0;
    }

    public static String getTransferJobError(int jobId) {
        TransferJob job = getTransferJob(jobId);
        return job != null ? job.error : "Transfer job not found";
    }

    public static void clearTransferJob(int jobId) {
        synchronized (TRANSFER_JOBS) {
            TRANSFER_JOBS.remove(jobId);
        }
    }

    private static boolean writeFileFromPath(
            Context context, String relativePath, String sourcePath, String mimeType, TransferJob job) {
        String normalized = normalize(relativePath);
        if (!isSafeRelativePath(normalized)) {
            setJobError(job, "Invalid shared-storage path");
            return false;
        }

        int slash = normalized.lastIndexOf('/');
        String directory = slash >= 0 ? normalized.substring(0, slash) : "";
        String fileName = slash >= 0 ? normalized.substring(slash + 1) : normalized;
        if (fileName.length() == 0) {
            setJobError(job, "Invalid shared-storage file name");
            return false;
        }

        Uri parent = ensureDirectoryUri(context, directory);
        Uri treeUri = getTreeUri(context);
        if (parent == null || treeUri == null) {
            setJobError(job, "Failed to open destination directory for " + relativePath);
            return false;
        }

        DocumentLookupResult existing = findChildDocumentUriResult(
                context, treeUri, parent, fileName);
        if (existing.error != null) {
            setJobError(job, existing.error);
            return false;
        }

        ContentResolver resolver = context.getContentResolver();
        String temporaryName = "." + fileName + ".openbrush-"
                + NEXT_TEMP_FILE_ID.getAndIncrement() + ".tmp";
        Uri temporary;
        try {
            temporary = DocumentsContract.createDocument(
                    resolver,
                    parent,
                    mimeType == null || mimeType.length() == 0
                            ? "application/octet-stream"
                            : mimeType,
                    temporaryName);
        } catch (Exception e) {
            setJobError(job, e.getMessage());
            return false;
        }
        if (temporary == null) {
            setJobError(job, "Failed to create temporary file for " + relativePath);
            return false;
        }

        try (InputStream input = new FileInputStream(sourcePath);
             OutputStream output = resolver.openOutputStream(temporary, "wt")) {
            if (output == null) {
                setJobError(job, "Failed to open " + relativePath);
                deleteDocumentQuietly(resolver, temporary);
                return false;
            }
            copyStream(input, output, job);
        } catch (Exception e) {
            setJobError(job, e.getMessage());
            deleteDocumentQuietly(resolver, temporary);
            return false;
        }

        Uri backup = null;
        if (existing.uri != null) {
            String backupName = "." + fileName + ".openbrush-backup-"
                    + NEXT_TEMP_FILE_ID.getAndIncrement();
            try {
                backup = DocumentsContract.renameDocument(resolver, existing.uri, backupName);
            } catch (Exception e) {
                setJobError(job, e.getMessage());
            }
            if (backup == null) {
                deleteDocumentQuietly(resolver, temporary);
                if (job == null || job.error.length() == 0) {
                    setJobError(job, "Failed to prepare existing file for replacement: " + relativePath);
                }
                return false;
            }
        }

        Uri replacement = null;
        try {
            replacement = DocumentsContract.renameDocument(resolver, temporary, fileName);
        } catch (Exception e) {
            setJobError(job, e.getMessage());
        }
        if (replacement == null) {
            if (backup != null) {
                try {
                    DocumentsContract.renameDocument(resolver, backup, fileName);
                } catch (Exception ignored) {
                    // The intact backup is retained if restoring its display name fails.
                }
            }
            deleteDocumentQuietly(resolver, temporary);
            if (job == null || job.error.length() == 0) {
                setJobError(job, "Failed to replace " + relativePath);
            }
            return false;
        }

        if (backup != null) {
            deleteDocumentQuietly(resolver, backup);
        }
        return true;
    }

    public static boolean copyDirectoryFromPath(
            Context context, String relativeDestinationPath, String sourceDirectoryPath) {
        return copyDirectoryFromPath(context, relativeDestinationPath, sourceDirectoryPath, null);
    }

    private static boolean copyDirectoryFromPath(
            Context context, String relativeDestinationPath, String sourceDirectoryPath, TransferJob job) {
        File source = new File(sourceDirectoryPath);
        if (!source.isDirectory()) {
            setJobError(job, "Source directory does not exist: " + sourceDirectoryPath);
            return false;
        }
        return copyDirectory(context, normalize(relativeDestinationPath), source, job);
    }

    public static boolean deleteTreeChild(Context context, String relativePath) {
        DocumentLookupResult targetLookup = findDocumentUriResult(
                context, normalize(relativePath));
        if (targetLookup.error != null) {
            return false;
        }
        if (targetLookup.uri == null) {
            return true;
        }
        try {
            return DocumentsContract.deleteDocument(
                    context.getContentResolver(), targetLookup.uri);
        } catch (Exception e) {
            return false;
        }
    }

    public static String[] listFiles(Context context, String relativePath) {
        Uri treeUri = getTreeUri(context);
        Uri directory = findDocumentUri(context, normalize(relativePath));
        if (treeUri == null || directory == null) {
            return new String[0];
        }

        ContentResolver resolver = context.getContentResolver();
        Uri childrenUri = DocumentsContract.buildChildDocumentsUriUsingTree(
                treeUri,
                DocumentsContract.getDocumentId(directory));
        ArrayList<String> names = new ArrayList<>();

        try (Cursor cursor = resolver.query(
                childrenUri,
                new String[]{DocumentsContract.Document.COLUMN_DISPLAY_NAME},
                null,
                null,
                null)) {
            while (cursor != null && cursor.moveToNext()) {
                names.add(cursor.getString(0));
            }
        } catch (Exception e) {
            return new String[0];
        }

        return names.toArray(new String[0]);
    }

    public static boolean copyFileToPath(
            Context context, String relativePath, String destinationPath) {
        Uri source = findDocumentUri(context, normalize(relativePath));
        if (source == null) {
            return false;
        }

        File destination = new File(destinationPath);
        File parent = destination.getParentFile();
        if (parent != null && !parent.exists() && !parent.mkdirs()) {
            return false;
        }

        try (InputStream input = context.getContentResolver().openInputStream(source);
             OutputStream output = new FileOutputStream(destination)) {
            if (input == null) {
                return false;
            }
            copyStream(input, output, null);
            return true;
        } catch (Exception e) {
            return false;
        }
    }

    public static boolean copyDirectoryToPath(
            Context context, String relativePath, String destinationDirectoryPath) {
        return copyDirectoryToPath(
                context, relativePath, destinationDirectoryPath, null, new HashSet<>());
    }

    private static boolean copyDirectoryToPath(
            Context context, String relativePath, String destinationDirectoryPath,
            TransferJob job, Set<String> preservedPaths) {
        Uri treeUri = getTreeUri(context);
        if (treeUri == null) {
            setJobError(job, "Open Brush folder is unavailable");
            return false;
        }

        DocumentLookupResult sourceLookup = findDocumentUriResult(
                context, normalize(relativePath));
        if (sourceLookup.error != null) {
            setJobError(job, sourceLookup.error);
            return false;
        }

        File destination = new File(destinationDirectoryPath);
        if (!destination.exists() && !destination.mkdirs()) {
            setJobError(job, "Failed to create local cache directory");
            return false;
        }
        if (sourceLookup.uri == null) {
            return reconcileLocalDirectory(destination, new HashSet<>(), preservedPaths);
        }

        return copyDocumentTreeToPath(
                context, treeUri, sourceLookup.uri, destination, job, preservedPaths);
    }
    static void saveOpenBrushFolderUri(Context context, String uriString) {
        SharedPreferences prefs = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
        prefs.edit().putString(OPEN_BRUSH_FOLDER_URI, uriString).apply();
    }

    private static String getOpenBrushFolderUri(Context context) {
        SharedPreferences prefs = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
        return prefs.getString(OPEN_BRUSH_FOLDER_URI, "");
    }

    private static Uri getTreeUri(Context context) {
        String uriString = getOpenBrushFolderUri(context);
        if (uriString == null || uriString.length() == 0) {
            return null;
        }
        return Uri.parse(uriString);
    }

    private static Uri getRootDocumentUri(Context context) {
        Uri treeUri = getTreeUri(context);
        if (treeUri == null) {
            return null;
        }
        return DocumentsContract.buildDocumentUriUsingTree(
                treeUri,
                DocumentsContract.getTreeDocumentId(treeUri));
    }

    private static boolean canQueryRoot(Context context) {
        Uri root = getRootDocumentUri(context);
        if (root == null) {
            return false;
        }

        try (Cursor cursor = context.getContentResolver().query(
                root,
                new String[]{DocumentsContract.Document.COLUMN_DOCUMENT_ID},
                null,
                null,
                null)) {
            return cursor != null && cursor.moveToFirst();
        } catch (Exception e) {
            return false;
        }
    }

    private static Uri ensureDirectoryUri(Context context, String relativePath) {
        Uri treeUri = getTreeUri(context);
        Uri current = getRootDocumentUri(context);
        if (treeUri == null || current == null) {
            return null;
        }

        String normalized = normalize(relativePath);
        if (!isSafeRelativePath(normalized)) {
            return null;
        }
        if (normalized.length() == 0) {
            return current;
        }

        for (String segment : normalized.split("/")) {
            Uri child = findChildDocumentUri(context, treeUri, current, segment);
            if (child == null) {
                try {
                    child = DocumentsContract.createDocument(
                            context.getContentResolver(),
                            current,
                            DocumentsContract.Document.MIME_TYPE_DIR,
                            segment);
                } catch (Exception e) {
                    return null;
                }
            }
            current = child;
        }
        return current;
    }

    private static void deleteDocumentQuietly(ContentResolver resolver, Uri document) {
        try {
            DocumentsContract.deleteDocument(resolver, document);
        } catch (Exception ignored) {
            // Best effort cleanup for temporary and backup documents.
        }
    }

    private static Uri findDocumentUri(Context context, String relativePath) {
        return findDocumentUriResult(context, relativePath).uri;
    }

    private static DescriptorOpenResult detachFileDescriptor(
            Context context, Uri documentUri, String mode) {
        ParcelFileDescriptor descriptor = null;
        try {
            descriptor = context.getContentResolver().openFileDescriptor(documentUri, mode);
            if (descriptor == null) {
                return new DescriptorOpenResult(
                        -1, documentUri, "Provider returned no file descriptor");
            }
            int fd = descriptor.detachFd();
            return new DescriptorOpenResult(fd, documentUri, null);
        } catch (Exception e) {
            return new DescriptorOpenResult(-1, documentUri, formatProviderError(
                    "Failed to open file descriptor", e));
        } finally {
            if (descriptor != null) {
                try {
                    descriptor.close();
                } catch (Exception ignored) {
                    // After detachFd(), closing the ParcelFileDescriptor object does not close
                    // the detached descriptor now owned by C#.
                }
            }
        }
    }

    private static boolean isSupportedDescriptorMode(String mode) {
        return "r".equals(mode)
                || "rw".equals(mode)
                || "rwt".equals(mode);
    }

    private static String formatProviderError(String prefix, Exception exception) {
        String detail = exception.getMessage();
        return detail == null || detail.length() == 0
                ? prefix
                : prefix + ": " + detail;
    }

    private static String emptyIfNull(String value) {
        return value == null ? "" : value;
    }

    private static DocumentLookupResult findDocumentUriResult(
            Context context, String relativePath) {
        Uri treeUri = getTreeUri(context);
        Uri current = getRootDocumentUri(context);
        if (treeUri == null || current == null) {
            return new DocumentLookupResult(null, "Open Brush folder is unavailable");
        }

        String normalized = normalize(relativePath);
        if (!isSafeRelativePath(normalized)) {
            return new DocumentLookupResult(null, "Invalid shared-storage path");
        }
        if (normalized.length() == 0) {
            return new DocumentLookupResult(current, null);
        }

        for (String segment : normalized.split("/")) {
            DocumentLookupResult child = findChildDocumentUriResult(
                    context, treeUri, current, segment);
            if (child.uri == null) {
                return child;
            }
            current = child.uri;
        }
        return new DocumentLookupResult(current, null);
    }

    private static Uri findChildDocumentUri(
            Context context, Uri treeUri, Uri parentDocumentUri, String displayName) {
        return findChildDocumentUriResult(
                context, treeUri, parentDocumentUri, displayName).uri;
    }

    private static DocumentLookupResult findChildDocumentUriResult(
            Context context, Uri treeUri, Uri parentDocumentUri, String displayName) {
        ContentResolver resolver = context.getContentResolver();
        Uri childrenUri = DocumentsContract.buildChildDocumentsUriUsingTree(
                treeUri,
                DocumentsContract.getDocumentId(parentDocumentUri));

        try (Cursor cursor = resolver.query(
                childrenUri,
                new String[]{
                        DocumentsContract.Document.COLUMN_DOCUMENT_ID,
                        DocumentsContract.Document.COLUMN_DISPLAY_NAME
                },
                null,
                null,
                null)) {
            if (cursor == null) {
                return new DocumentLookupResult(null, "Shared-storage query returned no result");
            }
            while (cursor.moveToNext()) {
                String childName = cursor.getString(1);
                if (displayName.equals(childName)) {
                    return new DocumentLookupResult(
                            DocumentsContract.buildDocumentUriUsingTree(
                                    treeUri, cursor.getString(0)),
                            null);
                }
            }
        } catch (Exception e) {
            String detail = e.getMessage();
            String error = detail == null || detail.length() == 0
                    ? "Failed to query shared storage"
                    : "Failed to query shared storage: " + detail;
            return new DocumentLookupResult(null, error);
        }
        return new DocumentLookupResult(null, null);
    }

    private static boolean copyDocumentTreeToPath(
            Context context, Uri treeUri, Uri sourceDocumentUri, File destinationDirectory,
            TransferJob job, Set<String> preservedPaths) {
        ContentResolver resolver = context.getContentResolver();
        Uri childrenUri = DocumentsContract.buildChildDocumentsUriUsingTree(
                treeUri,
                DocumentsContract.getDocumentId(sourceDocumentUri));
        Set<String> sharedChildNames = new HashSet<>();

        try (Cursor cursor = resolver.query(
                childrenUri,
                new String[]{
                        DocumentsContract.Document.COLUMN_DOCUMENT_ID,
                        DocumentsContract.Document.COLUMN_DISPLAY_NAME,
                        DocumentsContract.Document.COLUMN_MIME_TYPE
                },
                null,
                null,
                null)) {
            if (cursor == null) {
                setJobError(job, "Shared-storage query returned no result");
                return false;
            }
            while (cursor.moveToNext()) {
                Uri childUri = DocumentsContract.buildDocumentUriUsingTree(treeUri, cursor.getString(0));
                String childName = cursor.getString(1);
                String mimeType = cursor.getString(2);
                sharedChildNames.add(childName);
                if (DocumentsContract.Document.MIME_TYPE_DIR.equals(mimeType)) {
                    File childDirectory = new File(destinationDirectory, childName);
                    if (!childDirectory.exists() && !childDirectory.mkdirs()) {
                        return false;
                    }
                    if (!copyDocumentTreeToPath(
                            context, treeUri, childUri, childDirectory, job, preservedPaths)) {
                        return false;
                    }
                } else {
                    File childFile = new File(destinationDirectory, childName);
                    if (shouldPreserve(childFile, preservedPaths)) {
                        continue;
                    }
                    File parent = childFile.getParentFile();
                    if (parent != null && !parent.exists() && !parent.mkdirs()) {
                        return false;
                    }
                    try (InputStream input = resolver.openInputStream(childUri);
                         OutputStream output = new FileOutputStream(childFile)) {
                        if (input == null) {
                            return false;
                        }
                        copyStream(input, output, job);
                    }
                }
            }
        } catch (Exception e) {
            setJobError(job, e.getMessage());
            return false;
        }
        return reconcileLocalDirectory(destinationDirectory, sharedChildNames, preservedPaths);
    }

    private static boolean reconcileLocalDirectory(
            File directory, Set<String> sharedChildNames, Set<String> preservedPaths) {
        File[] localChildren = directory.listFiles();
        if (localChildren == null) {
            return true;
        }
        for (File child : localChildren) {
            if (!sharedChildNames.contains(child.getName()) && !shouldPreserve(child, preservedPaths)
                    && !deleteRecursively(child)) {
                return false;
            }
        }
        return true;
    }

    private static boolean shouldPreserve(File file, Set<String> preservedPaths) {
        String path = file.getAbsolutePath();
        String directoryPrefix = path + File.separator;
        for (String preservedPath : preservedPaths) {
            String preservedDirectoryPrefix = preservedPath + File.separator;
            if (preservedPath.equals(path) ||
                    preservedPath.startsWith(directoryPrefix) ||
                    path.startsWith(preservedDirectoryPrefix)) {
                return true;
            }
        }
        return false;
    }

    private static boolean deleteRecursively(File file) {
        if (file.isDirectory()) {
            File[] children = file.listFiles();
            if (children != null) {
                for (File child : children) {
                    if (!deleteRecursively(child)) {
                        return false;
                    }
                }
            }
        }
        return file.delete();
    }
    private static boolean copyDirectory(
            Context context, String relativeDestinationPath, File source, TransferJob job) {
        if (!ensureDirectory(context, relativeDestinationPath)) {
            setJobError(job, "Failed to create " + relativeDestinationPath);
            return false;
        }

        File[] children = source.listFiles();
        if (children == null) {
            return true;
        }

        for (File child : children) {
            String childRelativePath = relativeDestinationPath.length() == 0
                    ? child.getName()
                    : relativeDestinationPath + "/" + child.getName();
            if (child.isDirectory()) {
                if (!copyDirectory(context, childRelativePath, child, job)) {
                    return false;
                }
            } else if (!writeFileFromPath(
                    context,
                    childRelativePath,
                    child.getAbsolutePath(),
                    guessMimeType(child.getName()),
                    job)) {
                return false;
            }
        }
        return true;
    }

    private static TransferJob createTransferJob() {
        TransferJob job = new TransferJob();
        job.done = false;
        job.success = false;
        return job;
    }

    private static int registerTransferJob(TransferJob job) {
        int jobId = NEXT_TRANSFER_JOB_ID.getAndIncrement();
        synchronized (TRANSFER_JOBS) {
            TRANSFER_JOBS.put(jobId, job);
        }
        return jobId;
    }

    private static TransferJob getTransferJob(int jobId) {
        synchronized (TRANSFER_JOBS) {
            return TRANSFER_JOBS.get(jobId);
        }
    }

    private static void setJobError(TransferJob job, String error) {
        if (job != null && error != null && error.length() > 0) {
            job.error = error;
        }
    }

    private static long getFileLength(String path) {
        File file = new File(path);
        return file.isFile() ? file.length() : 0;
    }

    private static long countBytes(File file) {
        if (file == null || !file.exists()) {
            return 0;
        }
        if (file.isFile()) {
            return file.length();
        }
        long total = 0;
        File[] children = file.listFiles();
        if (children != null) {
            for (File child : children) {
                total += countBytes(child);
            }
        }
        return total;
    }

    private static String guessMimeType(String fileName) {
        String lower = fileName.toLowerCase();
        if (lower.endsWith(".txt")) {
            return "text/plain";
        }
        if (lower.endsWith(".png")) {
            return "image/png";
        }
        if (lower.endsWith(".jpg") || lower.endsWith(".jpeg")) {
            return "image/jpeg";
        }
        if (lower.endsWith(".json")) {
            return "application/json";
        }
        if (lower.endsWith(".glb")) {
            return "model/gltf-binary";
        }
        return "application/octet-stream";
    }

    private static String normalize(String path) {
        if (path == null) {
            return "";
        }
        String normalized = path.replace('\\', '/');
        while (normalized.startsWith("/")) {
            normalized = normalized.substring(1);
        }
        while (normalized.endsWith("/")) {
            normalized = normalized.substring(0, normalized.length() - 1);
        }
        return normalized;
    }

    private static boolean isSafeRelativePath(String path) {
        if (path == null || path.indexOf('\0') >= 0) {
            return false;
        }
        if (path.length() == 0) {
            return true;
        }
        for (String segment : path.split("/")) {
            if (segment.length() == 0 || ".".equals(segment) || "..".equals(segment)) {
                return false;
            }
        }
        return true;
    }

    private static void copyStream(InputStream input, OutputStream output, TransferJob job)
            throws java.io.IOException {
        byte[] buffer = new byte[1024 * 64];
        int read;
        while ((read = input.read(buffer)) >= 0) {
            output.write(buffer, 0, read);
            if (job != null) {
                job.bytesDone += read;
            }
        }
    }
}
