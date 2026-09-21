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

import com.unity3d.player.UnityPlayer;
import android.system.Os;
import android.system.StructStatVfs;

import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.nio.ByteBuffer;
import java.nio.channels.FileChannel;
import java.util.ArrayList;
import java.util.HashMap;
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

    // A handle into the channel table below, not a descriptor. Detaching the descriptor and
    // wrapping it in a managed FileStream segfaults IL2CPP inside the constructor, so the
    // descriptor stays here and C# issues positioned reads and writes against it.
    public static final class ChannelOpenResult {
        public final int handle;
        public final long length;
        public final String documentUri;
        public final String parentDocumentUri;
        public final String error;

        ChannelOpenResult(int handle, long length, Uri documentUri, String error) {
            this(handle, length, documentUri, null, error);
        }

        ChannelOpenResult(
                int handle, long length, Uri documentUri, Uri parentDocumentUri, String error) {
            this.handle = handle;
            this.length = length;
            this.documentUri = documentUri == null ? "" : documentUri.toString();
            this.parentDocumentUri = parentDocumentUri == null
                    ? ""
                    : parentDocumentUri.toString();
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

    public static boolean hasOpenBrushFolder() {
        Context context = resolveContext();
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
            clearOpenBrushFolder();
            return false;
        }

        if (!canQueryRoot(context)) {
            // A provider can be temporarily unavailable or return a null cursor. Preserve the
            // persisted identity so recovery work remains attached to the correct root.
            return false;
        }

        String displayName = getOpenBrushFolderDisplayName();
        if (OPEN_BRUSH_FOLDER_NAME.equals(displayName)) {
            return true;
        }
        if (displayName.length() > 0) {
            clearOpenBrushFolder();
        }
        return false;
    }

    public static String getOpenBrushFolderDisplayName() {
        Context context = resolveContext();
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

    public static void clearOpenBrushFolder() {
        Context context = resolveContext();
        SharedPreferences prefs = context.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE);
        prefs.edit().remove(OPEN_BRUSH_FOLDER_URI).apply();
    }

    public static String getSelectedRootIdentity() {
        Context context = resolveContext();
        return emptyIfNull(getOpenBrushFolderUri(context));
    }

    public static boolean ensureDirectory(String relativePath) {
        Context context = resolveContext();
        return ensureDirectoryUri(context, relativePath) != null;
    }

    public static DirectoryQueryResult queryDirectory(String relativePath) {
        Context context = resolveContext();
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

    public static ChannelOpenResult openChannelForPath(
            String relativePath, String mode) {
        Context context = resolveContext();
        if (!isSupportedChannelMode(mode)) {
            return new ChannelOpenResult(-1, -1, null, "Unsupported channel mode");
        }

        DocumentLookupResult lookup = findDocumentUriResult(
                context, normalize(relativePath));
        if (lookup.error != null) {
            return new ChannelOpenResult(-1, -1, null, lookup.error);
        }
        if (lookup.uri == null) {
            return new ChannelOpenResult(-1, -1, null, "Shared document does not exist");
        }

        return openChannel(context, lookup.uri, mode);
    }

    public static ChannelOpenResult openChannelForDocument(
            String documentUri, String mode) {
        Context context = resolveContext();
        if (!isSupportedChannelMode(mode)) {
            return new ChannelOpenResult(-1, -1, null, "Unsupported channel mode");
        }
        if (documentUri == null || documentUri.length() == 0) {
            return new ChannelOpenResult(-1, -1, null, "Document identity is empty");
        }
        try {
            return openChannel(context, Uri.parse(documentUri), mode);
        } catch (Exception e) {
            return new ChannelOpenResult(-1, -1, null, formatProviderError(
                    "Invalid document identity", e));
        }
    }

    public static ChannelOpenResult createTemporaryChannel(
            String relativeDirectory, String targetFileName, String mimeType) {
        Context context = resolveContext();
        String normalizedDirectory = normalize(relativeDirectory);
        if (!isSafeRelativePath(normalizedDirectory)
                || targetFileName == null
                || targetFileName.length() == 0
                || targetFileName.contains("/")
                || targetFileName.contains("\\")) {
            return new ChannelOpenResult(-1, -1, null, "Invalid temporary document path");
        }

        Uri parent = ensureDirectoryUri(context, normalizedDirectory);
        if (parent == null) {
            return new ChannelOpenResult(
                    -1, -1, null, "Failed to open temporary document directory");
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
            return new ChannelOpenResult(-1, -1, null, formatProviderError(
                    "Failed to create temporary document", e));
        }
        if (temporary == null) {
            return new ChannelOpenResult(
                    -1, -1, null, "Provider returned no temporary document");
        }

        ChannelOpenResult result = openChannel(context, temporary, "rwt");
        if (result.handle < 0) {
            deleteDocumentQuietly(context, temporary, parent);
        }
        return result;
    }

    public static ChannelOpenResult createNamedChannel(
            String relativeDirectory, String displayName, String mimeType) {
        Context context = resolveContext();
        String normalizedDirectory = normalize(relativeDirectory);
        if (!isSafeRelativePath(normalizedDirectory)
                || displayName == null
                || displayName.length() == 0
                || displayName.contains("/")
                || displayName.contains("\\")) {
            return new ChannelOpenResult(-1, -1, null, "Invalid document path");
        }

        Uri parent = ensureDirectoryUri(context, normalizedDirectory);
        if (parent == null) {
            return new ChannelOpenResult(-1, -1, null, "Failed to open document directory");
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
                return new ChannelOpenResult(-1, -1, null, "Provider returned no document");
            }
            ChannelOpenResult result = openChannel(context, document, "rwt");
            if (result.handle < 0) {
                deleteDocumentQuietly(context, document, parent);
            }
            return new ChannelOpenResult(
                    result.handle, result.length, document, parent, result.error);
        } catch (Exception e) {
            return new ChannelOpenResult(-1, -1, null, formatProviderError(
                    "Failed to create document", e));
        }
    }

    // Bytes available on the volume holding the Open Brush folder, or -1 when it cannot be
    // determined. There is no path to stat - the folder is a provider grant, not a directory - and
    // openFileDescriptor refuses a directory document, so this measures the filesystem underneath
    // a throwaway document created in the folder itself. That is the volume a save actually lands
    // on, which StatFs on the app's private directory is not: the folder may be on an SD card.
    public static long getAvailableBytes() {
        Context context = resolveContext();
        Uri root = getRootDocumentUri(context);
        if (root == null) {
            return -1;
        }
        Uri probe = null;
        ParcelFileDescriptor descriptor = null;
        try {
            probe = DocumentsContract.createDocument(
                    context.getContentResolver(),
                    root,
                    "application/octet-stream",
                    ".openbrush-space-" + NEXT_TEMP_FILE_ID.getAndIncrement() + ".tmp");
            if (probe == null) {
                return -1;
            }
            descriptor = context.getContentResolver().openFileDescriptor(probe, "rw");
            if (descriptor == null) {
                return -1;
            }
            StructStatVfs stat = Os.fstatvfs(descriptor.getFileDescriptor());
            return stat.f_bavail * stat.f_frsize;
        } catch (Exception e) {
            // A provider that is not backed by a local filesystem cannot answer this. Reporting
            // -1 leaves the caller to allow the save rather than block it on an unknown.
            return -1;
        } finally {
            if (descriptor != null) {
                try {
                    descriptor.close();
                } catch (Exception ignored) {
                    // Nothing useful is left to do with a descriptor that will not close.
                }
            }
            if (probe != null) {
                deleteDocumentQuietly(context, probe, root);
            }
        }
    }

    // Positioned read. Returns the bytes actually read - a short array at end of file, an empty
    // array at or past it - or null when the read failed, in which case channelError describes it.
    public static byte[] readChannel(int handle, long position, int length) {
        ChannelEntry entry = findChannel(handle);
        if (entry == null) {
            return null;
        }
        if (length <= 0) {
            return new byte[0];
        }
        try {
            if (entry.read == null) {
                throw new IllegalStateException("Channel is not open for reading");
            }
            ByteBuffer buffer = ByteBuffer.allocate(length);
            int total = 0;
            while (total < length) {
                int read = entry.read.read(buffer, position + total);
                if (read <= 0) {
                    break;
                }
                total += read;
            }
            entry.error = null;
            if (total == length) {
                return buffer.array();
            }
            byte[] result = new byte[total];
            System.arraycopy(buffer.array(), 0, result, 0, total);
            return result;
        } catch (Exception e) {
            entry.error = formatProviderError("Failed to read the shared document", e);
            return null;
        }
    }

    // Positioned write. Returns the number of bytes written, or -1 on failure.
    public static int writeChannel(int handle, long position, byte[] data) {
        ChannelEntry entry = findChannel(handle);
        if (entry == null) {
            return -1;
        }
        if (data == null || data.length == 0) {
            return 0;
        }
        try {
            if (entry.write == null) {
                throw new IllegalStateException("Channel is not open for writing");
            }
            ByteBuffer buffer = ByteBuffer.wrap(data);
            int total = 0;
            while (buffer.hasRemaining()) {
                int written = entry.write.write(buffer, position + total);
                if (written <= 0) {
                    break;
                }
                total += written;
            }
            entry.error = null;
            return total;
        } catch (Exception e) {
            entry.error = formatProviderError("Failed to write the shared document", e);
            return -1;
        }
    }

    public static boolean truncateChannel(int handle, long length) {
        ChannelEntry entry = findChannel(handle);
        if (entry == null) {
            return false;
        }
        try {
            if (entry.write == null) {
                throw new IllegalStateException("Channel is not open for writing");
            }
            entry.write.truncate(length);
            entry.error = null;
            return true;
        } catch (Exception e) {
            entry.error = formatProviderError("Failed to resize the shared document", e);
            return false;
        }
    }

    // toDisk is an fsync, not a flush: it is what makes a committed payload survive power loss,
    // and it is the only reason this is separate from the positioned writes above.
    public static boolean flushChannel(int handle, boolean toDisk) {
        ChannelEntry entry = findChannel(handle);
        if (entry == null) {
            return false;
        }
        try {
            if (entry.write != null) {
                entry.write.force(toDisk);
            }
            entry.error = null;
            return true;
        } catch (Exception e) {
            entry.error = formatProviderError("Failed to flush the shared document", e);
            return false;
        }
    }

    public static String channelError(int handle) {
        ChannelEntry entry = findChannel(handle);
        if (entry == null) {
            return "The shared document channel is closed";
        }
        return entry.error == null ? "" : entry.error;
    }

    public static void closeChannel(int handle) {
        ChannelEntry entry;
        synchronized (CHANNELS) {
            entry = CHANNELS.remove(handle);
        }
        if (entry != null) {
            entry.close();
        }
    }

    public static DocumentMutationResult renameDocumentUri(
            String documentUri, String newDisplayName) {
        Context context = resolveContext();
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
            String documentUri, String parentDocumentUri) {
        Context context = resolveContext();
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

    public static boolean deleteDocumentUri(String documentUri) {
        Context context = resolveContext();
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

    // Resolved here rather than passed in from C#. UnityPlayer.currentActivity is populated
    // late in startup and is null before that, and a null AndroidJavaObject cannot be marshalled
    // into a JNI argument array - Unity throws NullReferenceException building the array, which
    // surfaced as a quiet failure in every storage call made before the activity existed.
    // Reading the field here is a direct field access with no signature inference: currentContext
    // is the application context and is set earlier than currentActivity.
    private static Context resolveContext() {
        Context activity = UnityPlayer.currentActivity;
        return activity != null ? activity : UnityPlayer.currentContext;
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

    private static void deleteDocumentQuietly(Context context, Uri document, Uri parent) {
        try {
            FlagLookupResult capability = lookupDocumentFlags(context, document);
            if (capability.error == null
                    && (capability.flags
                    & DocumentsContract.Document.FLAG_SUPPORTS_DELETE) == 0
                    && (capability.flags
                    & DocumentsContract.Document.FLAG_SUPPORTS_REMOVE) != 0
                    && parent != null) {
                DocumentsContract.removeDocument(
                        context.getContentResolver(), document, parent);
            } else {
                // Preserve the original best-effort delete for providers that do not expose
                // reliable flags, as well as the ordinary FLAG_SUPPORTS_DELETE case.
                DocumentsContract.deleteDocument(context.getContentResolver(), document);
            }
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

    private static final class ChannelEntry {
        final ParcelFileDescriptor descriptor;
        final FileInputStream input;
        final FileOutputStream output;
        final FileChannel read;
        final FileChannel write;
        volatile String error;

        ChannelEntry(ParcelFileDescriptor descriptor,
                     FileInputStream input,
                     FileOutputStream output) {
            this.descriptor = descriptor;
            this.input = input;
            this.output = output;
            this.read = input == null ? null : input.getChannel();
            this.write = output == null ? null : output.getChannel();
        }

        void close() {
            closeQuietly(read);
            closeQuietly(write);
            closeQuietly(input);
            closeQuietly(output);
            closeQuietly(descriptor);
        }

        private static void closeQuietly(java.io.Closeable closeable) {
            if (closeable == null) {
                return;
            }
            try {
                closeable.close();
            } catch (Exception ignored) {
                // Nothing useful is left to do with a channel that will not close.
            }
        }
    }

    private static final HashMap<Integer, ChannelEntry> CHANNELS =
            new HashMap<Integer, ChannelEntry>();
    private static final AtomicInteger NEXT_CHANNEL_HANDLE = new AtomicInteger(1);

    private static ChannelEntry findChannel(int handle) {
        synchronized (CHANNELS) {
            return CHANNELS.get(handle);
        }
    }

    private static ChannelOpenResult openChannel(Context context, Uri documentUri, String mode) {
        ParcelFileDescriptor descriptor = null;
        try {
            descriptor = context.getContentResolver().openFileDescriptor(documentUri, mode);
            if (descriptor == null) {
                return new ChannelOpenResult(
                        -1, -1, documentUri, "Provider returned no file descriptor");
            }
            // Both streams wrap the same descriptor, so they share nothing but its identity: every
            // read and write below is positioned and ignores the shared file offset.
            FileInputStream input = new FileInputStream(descriptor.getFileDescriptor());
            FileOutputStream output = mode.indexOf('w') < 0
                    ? null
                    : new FileOutputStream(descriptor.getFileDescriptor());
            ChannelEntry entry = new ChannelEntry(descriptor, input, output);
            long length = entry.read.size();
            int handle = NEXT_CHANNEL_HANDLE.getAndIncrement();
            synchronized (CHANNELS) {
                CHANNELS.put(handle, entry);
            }
            descriptor = null;
            return new ChannelOpenResult(handle, length, documentUri, null);
        } catch (Exception e) {
            return new ChannelOpenResult(-1, -1, documentUri, formatProviderError(
                    "Failed to open the shared document", e));
        } finally {
            if (descriptor != null) {
                try {
                    descriptor.close();
                } catch (Exception ignored) {
                    // The channel table never took ownership, so this is the only close left.
                }
            }
        }
    }

    private static boolean isSupportedChannelMode(String mode) {
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
