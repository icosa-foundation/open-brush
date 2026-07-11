package foundation.icosa.openbrush.storage;

import android.app.Activity;
import android.content.ContentResolver;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.database.Cursor;
import android.net.Uri;
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
    private static final Map<Integer, TransferJob> TRANSFER_JOBS = new HashMap<>();

    private static class TransferJob {
        volatile boolean done;
        volatile boolean success;
        volatile long bytesDone;
        volatile long bytesTotal;
        volatile String error = "";
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
        Uri target = createFileUri(context, relativePath, mimeType);
        if (target == null) {
            setJobError(job, "Failed to create " + relativePath);
            return false;
        }

        try (InputStream input = new FileInputStream(sourcePath);
             OutputStream output = context.getContentResolver().openOutputStream(target, "wt")) {
            if (output == null) {
                setJobError(job, "Failed to open " + relativePath);
                return false;
            }
            copyStream(input, output, job);
            return true;
        } catch (Exception e) {
            setJobError(job, e.getMessage());
            return false;
        }
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
        Uri target = findDocumentUri(context, normalize(relativePath));
        if (target == null) {
            return true;
        }
        try {
            return DocumentsContract.deleteDocument(context.getContentResolver(), target);
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
        Uri source = findDocumentUri(context, normalize(relativePath));
        Uri treeUri = getTreeUri(context);
        if (treeUri == null) {
            setJobError(job, "Open Brush folder is unavailable");
            return false;
        }

        File destination = new File(destinationDirectoryPath);
        if (!destination.exists() && !destination.mkdirs()) {
            setJobError(job, "Failed to create local cache directory");
            return false;
        }
        if (source == null) {
            return reconcileLocalDirectory(destination, new HashSet<>(), preservedPaths);
        }

        return copyDocumentTreeToPath(
                context, treeUri, source, destination, job, preservedPaths);
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

    private static Uri createFileUri(Context context, String relativePath, String mimeType) {
        String normalized = normalize(relativePath);
        if (!isSafeRelativePath(normalized)) {
            return null;
        }
        int slash = normalized.lastIndexOf('/');
        String directory = slash >= 0 ? normalized.substring(0, slash) : "";
        String fileName = slash >= 0 ? normalized.substring(slash + 1) : normalized;
        if (fileName.length() == 0) {
            return null;
        }

        Uri parent = ensureDirectoryUri(context, directory);
        Uri treeUri = getTreeUri(context);
        if (parent == null || treeUri == null) {
            return null;
        }

        Uri existing = findChildDocumentUri(context, treeUri, parent, fileName);
        if (existing != null) {
            return existing;
        }

        try {
            return DocumentsContract.createDocument(
                    context.getContentResolver(),
                    parent,
                    mimeType == null || mimeType.length() == 0 ? "application/octet-stream" : mimeType,
                    fileName);
        } catch (Exception e) {
            return null;
        }
    }

    private static Uri findDocumentUri(Context context, String relativePath) {
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
            current = findChildDocumentUri(context, treeUri, current, segment);
            if (current == null) {
                return null;
            }
        }
        return current;
    }

    private static Uri findChildDocumentUri(
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
            while (cursor != null && cursor.moveToNext()) {
                String childName = cursor.getString(1);
                if (displayName.equals(childName)) {
                    return DocumentsContract.buildDocumentUriUsingTree(treeUri, cursor.getString(0));
                }
            }
        } catch (Exception e) {
            return null;
        }
        return null;
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
            while (cursor != null && cursor.moveToNext()) {
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
            if (preservedPath.equals(path) || preservedPath.startsWith(directoryPrefix)) {
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
