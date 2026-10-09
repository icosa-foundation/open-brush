// Copyright 2026 The Open Brush Authors. Licensed under Apache-2.0.
package foundation.icosa.obds;

import android.app.Activity;
import android.content.Intent;
import android.content.UriPermission;
import android.net.Uri;
import android.media.MediaScannerConnection;
import android.os.Environment;
import android.os.ParcelFileDescriptor;
import android.provider.DocumentsContract;
import android.provider.MediaStore;
import com.unity3d.player.UnityPlayer;
import com.unity3d.player.UnityPlayerActivity;
import java.io.IOException;
import java.nio.ByteBuffer;
import java.nio.channels.FileChannel;
import java.util.Arrays;

public class ProbeActivity extends UnityPlayerActivity {
    public void scanStartupJson() {
        String path = documentsPath() + "/OBDS_StartupGate20261009/externally-added-after-startup.json";
        MediaScannerConnection.scanFile(this, new String[] {path}, null,
            (scanned, uri) -> android.util.Log.i("OBDS_SCAN", "completed uri=" + uri));
    }

    public String documentsPath() {
        return Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOCUMENTS).getAbsolutePath();
    }

    public String environment() {
        StringBuilder grants = new StringBuilder();
        for (UriPermission grant : getContentResolver().getPersistedUriPermissions())
            grants.append(grant.getUri()).append(" read=").append(grant.isReadPermission())
                .append(" write=").append(grant.isWritePermission()).append(";");
        return "sdk=" + android.os.Build.VERSION.SDK_INT + " fingerprint=" + android.os.Build.FINGERPRINT
            + " uid=" + android.os.Process.myUid() + " target=" + getApplicationInfo().targetSdkVersion
            + " manager=" + Environment.isExternalStorageManager()
            + " legacy=" + Environment.isExternalStorageLegacy() + " grants=" + grants;
    }

    public String persistedTree() {
        for (UriPermission grant : getContentResolver().getPersistedUriPermissions())
            if (grant.isReadPermission()) return grant.getUri().toString();
        return "";
    }

    public void pickTree() {
        pickTree(false);
    }

    public void pickWritableTestTree() {
        pickTree(true);
    }

    private void pickTree(boolean writable) {
        runOnUiThread(() -> {
            Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT_TREE);
            intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_GRANT_PERSISTABLE_URI_PERMISSION);
            if (writable) intent.addFlags(Intent.FLAG_GRANT_WRITE_URI_PERMISSION);
            String previous = persistedTree();
            if (!previous.isEmpty()) intent.putExtra(DocumentsContract.EXTRA_INITIAL_URI, Uri.parse(previous));
            startActivityForResult(intent, 7419);
        });
    }

    @Override protected void onActivityResult(int request, int result, Intent data) {
        super.onActivityResult(request, result, data);
        if (request != 7419) return;
        if (result != Activity.RESULT_OK || data == null || data.getData() == null) {
            UnityPlayer.UnitySendMessage("OBDS_Probe", "OnTree", "");
            return;
        }
        Uri tree = data.getData();
        try {
            getContentResolver().takePersistableUriPermission(tree,
                data.getFlags() & (Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_GRANT_WRITE_URI_PERMISSION));
            UnityPlayer.UnitySendMessage("OBDS_Probe", "OnTree", tree.toString());
        } catch (Exception failure) {
            UnityPlayer.UnitySendMessage("OBDS_Probe", "OnTreeError", failure.toString());
        }
    }

    public boolean writableTestTree(String treeText) {
        Uri tree = Uri.parse(treeText);
        if (!"com.android.externalstorage.documents".equals(tree.getAuthority())) return false;
        if (!DocumentsContract.getTreeDocumentId(tree).equals(
                "primary:Documents/OBDS_ExternalFixture20261009_1347")) return false;
        for (UriPermission grant : getContentResolver().getPersistedUriPermissions())
            if (grant.getUri().toString().equals(treeText) && grant.isWritePermission()) return true;
        return false;
    }

    public String testDocument(String treeText, String name) throws Exception {
        if (!writableTestTree(treeText) || !name.startsWith("OBDS_"))
            throw new IOException("Write experiment is restricted to its own test folder and fixtures");
        Uri tree = Uri.parse(treeText);
        Uri children = DocumentsContract.buildChildDocumentsUriUsingTree(tree,
            DocumentsContract.getTreeDocumentId(tree));
        try (android.database.Cursor cursor = getContentResolver().query(children,
            new String[] { DocumentsContract.Document.COLUMN_DOCUMENT_ID,
                DocumentsContract.Document.COLUMN_DISPLAY_NAME }, null, null, null)) {
            if (cursor == null) throw new IOException("Null tree cursor");
            while (cursor.moveToNext())
                if (name.equals(cursor.getString(1)))
                    return DocumentsContract.buildDocumentUriUsingTree(tree, cursor.getString(0)).toString();
        }
        throw new IOException("Test document not found: " + name);
    }

    public String renameTestDocument(String treeText, String uriText, String name) throws Exception {
        if (!writableTestTree(treeText) || !name.startsWith("OBDS_"))
            throw new IOException("Rename outside the test namespace is prohibited");
        Uri source = Uri.parse(uriText);
        if (!DocumentsContract.getDocumentId(source).startsWith(
                "primary:Documents/OBDS_ExternalFixture20261009_1347/OBDS_"))
            throw new IOException("Not a probe-created transaction fixture");
        Uri renamed = DocumentsContract.renameDocument(getContentResolver(), source, name);
        if (renamed == null) throw new IOException("Provider returned no renamed URI");
        return renamed.toString();
    }

    // A tree grant, rather than a separate file grant, is the subject of this experiment.
    public String[] children(String treeText) throws Exception {
        Uri tree = Uri.parse(treeText);
        Uri children = DocumentsContract.buildChildDocumentsUriUsingTree(tree,
            DocumentsContract.getTreeDocumentId(tree));
        java.util.ArrayList<String> results = new java.util.ArrayList<>();
        try (android.database.Cursor cursor = getContentResolver().query(children,
            new String[] { DocumentsContract.Document.COLUMN_DOCUMENT_ID,
                DocumentsContract.Document.COLUMN_DISPLAY_NAME }, null, null, null)) {
            if (cursor == null) throw new IOException("Null tree cursor");
            while (cursor.moveToNext()) {
                if (!cursor.getString(1).endsWith(".tilt")) continue;
                results.add(DocumentsContract.buildDocumentUriUsingTree(tree, cursor.getString(0)).toString());
            }
        }
        return results.toArray(new String[0]);
    }

    public String localPath(String uriText) {
        String id = DocumentsContract.getDocumentId(Uri.parse(uriText));
        if (!id.startsWith("primary:")) return "";
        return Environment.getExternalStorageDirectory().getAbsolutePath() + "/" + id.substring(8);
    }

    public String mediaUri(String uriText) {
        Uri translated = MediaStore.getMediaUri(this, Uri.parse(uriText));
        return translated == null ? "" : translated.toString();
    }

    public ReadChannel openRead(String uriText) throws Exception {
        return new ReadChannel(getContentResolver().openFileDescriptor(Uri.parse(uriText), "r"));
    }

    public static final class ReadChannel {
        private final ParcelFileDescriptor.AutoCloseInputStream input;
        private final FileChannel channel;
        private final int fd;

        ReadChannel(ParcelFileDescriptor descriptor) throws Exception {
            if (descriptor == null) throw new IOException("Null provider descriptor");
            fd = descriptor.getFd();
            input = new ParcelFileDescriptor.AutoCloseInputStream(descriptor);
            channel = input.getChannel();
            try {
                channel.size();
                channel.position(0); // Reject pipes before exposing a seekable stream.
            } catch (Exception failure) {
                input.close();
                throw failure;
            }
        }

        public long length() throws IOException { return channel.size(); }
        public String alias() { return "/proc/self/fd/" + fd; }
        public byte[] read(long position, int count) throws IOException {
            if (position < 0 || count < 0 || count > 65536) throw new IOException("Invalid read range");
            byte[] data = new byte[count];
            ByteBuffer buffer = ByteBuffer.wrap(data);
            while (buffer.hasRemaining()) {
                int n = channel.read(buffer, position + buffer.position());
                if (n < 0) break;
                if (n == 0) throw new IOException("Provider made no progress");
            }
            return buffer.position() == count ? data : Arrays.copyOf(data, buffer.position());
        }
        public void close() throws IOException { input.close(); }
    }
}
