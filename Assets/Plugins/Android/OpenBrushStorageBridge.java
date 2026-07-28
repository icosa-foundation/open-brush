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

import java.util.ArrayList;
import java.util.concurrent.atomic.AtomicInteger;

public class OpenBrushStorageBridge {
    private static final String PREFS_NAME = "OpenBrushStorage";
    private static final String OPEN_BRUSH_FOLDER_URI = "openBrushFolderUri";
    private static final String OPEN_BRUSH_FOLDER_NAME = "Open Brush";
    private static final AtomicInteger NEXT_TEMP_FILE_ID = new AtomicInteger(1);
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

    public static final class DocumentMutationResult {
        public final int code;
        public final String documentUri;
        public final String error;

        DocumentMutationResult(int code, Uri documentUri, String error) {
            this.code = code;
            this.documentUri = documentUri == null ? "" : documentUri.toString();
            this.error = error == null ? "" : error;
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

    private static final class FlagLookupResult {
        final long flags;
        final String error;

        FlagLookupResult(long flags, String error) {
            this.flags = flags;
            this.error = error;
        }
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

        if (!hasPersistedGrant) {
            clearOpenBrushFolder(context);
            return false;
        }

        if (!canQueryRoot(context)) {
            // A provider can be temporarily unavailable or return a null cursor. Preserve the
            // persisted identity so recovery work remains attached to the correct root.
            return false;
        }

        String displayName = getOpenBrushFolderDisplayName(context);
        if (OPEN_BRUSH_FOLDER_NAME.equals(displayName)) {
            return true;
        }
        if (displayName.length() > 0) {
            clearOpenBrushFolder(context);
        }
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

    public static String getSelectedRootIdentity(Context context) {
        return emptyIfNull(getOpenBrushFolderUri(context));
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

    public static DescriptorOpenResult openDocumentFileDescriptor(
            Context context, String documentUri, String mode) {
        if (!isSupportedDescriptorMode(mode)) {
            return new DescriptorOpenResult(-1, null, "Unsupported file descriptor mode");
        }
        if (documentUri == null || documentUri.length() == 0) {
            return new DescriptorOpenResult(-1, null, "Document identity is empty");
        }
        try {
            return detachFileDescriptor(context, Uri.parse(documentUri), mode);
        } catch (Exception e) {
            return new DescriptorOpenResult(-1, null, formatProviderError(
                    "Invalid document identity", e));
        }
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

    public static DescriptorOpenResult createNamedFileDescriptor(
            Context context, String relativeDirectory, String displayName, String mimeType) {
        String normalizedDirectory = normalize(relativeDirectory);
        if (!isSafeRelativePath(normalizedDirectory)
                || displayName == null
                || displayName.length() == 0
                || displayName.contains("/")
                || displayName.contains("\\")) {
            return new DescriptorOpenResult(-1, null, "Invalid document path");
        }

        Uri parent = ensureDirectoryUri(context, normalizedDirectory);
        if (parent == null) {
            return new DescriptorOpenResult(-1, null, "Failed to open document directory");
        }
        try {
            Uri document = DocumentsContract.createDocument(
                    context.getContentResolver(),
                    parent,
                    mimeType == null || mimeType.length() == 0
                            ? "application/octet-stream"
                            : mimeType,
                    displayName);
            if (document == null) {
                return new DescriptorOpenResult(-1, null, "Provider returned no document");
            }
            DescriptorOpenResult result = detachFileDescriptor(context, document, "rwt");
            if (result.fd < 0) {
                deleteDocumentQuietly(context.getContentResolver(), document);
            }
            return result;
        } catch (Exception e) {
            return new DescriptorOpenResult(-1, null, formatProviderError(
                    "Failed to create document", e));
        }
    }

    public static DocumentMutationResult renameDocumentUri(
            Context context, String documentUri, String newDisplayName) {
        if (documentUri == null
                || documentUri.length() == 0
                || newDisplayName == null
                || newDisplayName.length() == 0
                || newDisplayName.contains("/")
                || newDisplayName.contains("\\")) {
            return new DocumentMutationResult(6, null, "Invalid rename request");
        }
        try {
            Uri source = Uri.parse(documentUri);
            FlagLookupResult capability = lookupDocumentFlags(context, source);
            if (capability.error != null) {
                return new DocumentMutationResult(5, null, capability.error);
            }
            if ((capability.flags
                    & DocumentsContract.Document.FLAG_SUPPORTS_RENAME) == 0) {
                return new DocumentMutationResult(
                        7, null, "Provider does not support renaming this document");
            }
            Uri renamed = DocumentsContract.renameDocument(
                    context.getContentResolver(), source, newDisplayName);
            if (renamed == null) {
                return new DocumentMutationResult(
                        7, null, "Provider returned no renamed document");
            }
            return new DocumentMutationResult(0, renamed, null);
        } catch (SecurityException e) {
            return new DocumentMutationResult(3, null, formatProviderError(
                    "Permission denied while renaming document", e));
        } catch (Exception e) {
            return new DocumentMutationResult(7, null, formatProviderError(
                    "Failed to rename document", e));
        }
    }

    public static DocumentMutationResult deleteDocumentByUri(
            Context context, String documentUri, String parentDocumentUri) {
        if (documentUri == null || documentUri.length() == 0) {
            return new DocumentMutationResult(6, null, "Invalid delete request");
        }
        try {
            Uri document = Uri.parse(documentUri);
            FlagLookupResult capability = lookupDocumentFlags(context, document);
            if (capability.error != null) {
                return new DocumentMutationResult(5, null, capability.error);
            }
            boolean deleted;
            if ((capability.flags
                    & DocumentsContract.Document.FLAG_SUPPORTS_DELETE) != 0) {
                deleted = DocumentsContract.deleteDocument(
                        context.getContentResolver(), document);
            } else if ((capability.flags
                    & DocumentsContract.Document.FLAG_SUPPORTS_REMOVE) != 0
                    && parentDocumentUri != null
                    && parentDocumentUri.length() > 0) {
                DocumentsContract.removeDocument(
                        context.getContentResolver(),
                        document,
                        Uri.parse(parentDocumentUri));
                deleted = true;
            } else {
                return new DocumentMutationResult(
                        7, null, "Provider does not support deleting this document");
            }
            return deleted
                    ? new DocumentMutationResult(0, document, null)
                    : new DocumentMutationResult(7, null, "Provider did not delete document");
        } catch (SecurityException e) {
            return new DocumentMutationResult(3, null, formatProviderError(
                    "Permission denied while deleting document", e));
        } catch (Exception e) {
            return new DocumentMutationResult(7, null, formatProviderError(
                    "Failed to delete document", e));
        }
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

    private static FlagLookupResult lookupDocumentFlags(Context context, Uri document) {
        try (Cursor cursor = context.getContentResolver().query(
                document,
                new String[]{DocumentsContract.Document.COLUMN_FLAGS},
                null,
                null,
                null)) {
            if (cursor == null) {
                return new FlagLookupResult(
                        0, "Provider returned no document capability result");
            }
            if (!cursor.moveToFirst()) {
                return new FlagLookupResult(
                        0, "Provider document no longer exists");
            }
            return new FlagLookupResult(cursor.isNull(0) ? 0 : cursor.getLong(0), null);
        } catch (Exception e) {
            return new FlagLookupResult(0, formatProviderError(
                    "Failed to query document capabilities", e));
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

    private static String normalize(String path) {
        if (path == null) {
            return "";
        }
        return path.replace('\\', '/');
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

}
