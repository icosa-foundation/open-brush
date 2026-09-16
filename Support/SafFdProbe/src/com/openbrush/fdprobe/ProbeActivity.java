package com.openbrush.fdprobe;

import android.app.Activity;
import android.content.Intent;
import android.database.Cursor;
import android.net.Uri;
import android.os.Bundle;
import android.os.ParcelFileDescriptor;
import android.provider.DocumentsContract;
import android.system.Os;
import android.system.OsConstants;
import android.system.StructStat;
import android.util.Log;
import android.widget.ScrollView;
import android.widget.TextView;

import java.io.FileDescriptor;
import java.io.RandomAccessFile;
import java.util.Random;

/**
 * Standalone probe for the assumptions behind the fd-backed SAF design.
 * Mirrors AndroidSafStorage.RunFileDescriptorProbe without Unity or IL2CPP.
 */
public class ProbeActivity extends Activity {

    private static final String TAG = "OBFDPROBE";
    private static final int REQ_TREE = 1;
    private static final int PAYLOAD = 3 * 1024 * 1024;

    private TextView mOut;
    private StringBuilder mLog = new StringBuilder();

    @Override protected void onCreate(Bundle b) {
        super.onCreate(b);
        mOut = new TextView(this);
        mOut.setTextSize(11f);
        ScrollView sv = new ScrollView(this);
        sv.addView(mOut);
        setContentView(sv);

        Intent i = new Intent(Intent.ACTION_OPEN_DOCUMENT_TREE);
        i.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION
                | Intent.FLAG_GRANT_WRITE_URI_PERMISSION
                | Intent.FLAG_GRANT_PERSISTABLE_URI_PERMISSION);
        startActivityForResult(i, REQ_TREE);
    }

    @Override protected void onActivityResult(int req, int res, Intent data) {
        super.onActivityResult(req, res, data);
        if (req != REQ_TREE || res != RESULT_OK || data == null || data.getData() == null) {
            say("ABORT: no folder selected");
            return;
        }
        Uri tree = data.getData();
        getContentResolver().takePersistableUriPermission(tree,
                Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_GRANT_WRITE_URI_PERMISSION);
        say("tree = " + tree);
        say("authority = " + tree.getAuthority());
        try {
            runProbe(tree);
        } catch (Throwable t) {
            say("FATAL " + t.getClass().getName() + ": " + t.getMessage());
            Log.e(TAG, "probe failed", t);
        }
    }

    private void runProbe(Uri tree) throws Exception {
        Uri dir = DocumentsContract.buildDocumentUriUsingTree(
                tree, DocumentsContract.getTreeDocumentId(tree));

        // --- 1. create a document -------------------------------------------------
        Uri doc = DocumentsContract.createDocument(
                getContentResolver(), dir, "application/octet-stream", "obfdprobe.tilt");
        if (doc == null) { say("FAIL 1 createDocument returned null"); return; }
        say("PASS 1 createDocument -> " + doc.getLastPathSegment());

        // --- 2. provider capability flags ----------------------------------------
        int flags = queryFlags(doc);
        say("INFO 2 flags=0x" + Integer.toHexString(flags)
                + " write=" + has(flags, DocumentsContract.Document.FLAG_SUPPORTS_WRITE)
                + " rename=" + has(flags, DocumentsContract.Document.FLAG_SUPPORTS_RENAME)
                + " delete=" + has(flags, DocumentsContract.Document.FLAG_SUPPORTS_DELETE));

        // --- 3. detachFd + seekability -------------------------------------------
        byte[] payload = new byte[PAYLOAD];
        new Random(42).nextBytes(payload);

        Fd fd = detach(doc, "rw");
        if (fd == null) { say("FAIL 3 openFileDescriptor rw returned nothing"); return; }
        say("PASS 3 detachFd -> " + fd.raw);

        try {
            StructStat st = Os.fstat(fd.jfd);
            boolean reg = OsConstants.S_ISREG(st.st_mode);
            say("INFO 3a fstat mode=0" + Integer.toOctalString(st.st_mode)
                    + " regular=" + reg + " size=" + st.st_size);
            if (!reg) say("WARN 3a not a regular file - pipe/socket, seeking will fail");

            long end = Os.lseek(fd.jfd, 0, OsConstants.SEEK_END);
            say("PASS 3b lseek(SEEK_END) = " + end);
            Os.lseek(fd.jfd, 0, OsConstants.SEEK_SET);

            // --- 4. write the payload through the detached fd --------------------
            int off = 0;
            while (off < payload.length) off += Os.write(fd.jfd, payload, off, payload.length - off);
            say("PASS 4 wrote " + off + " bytes through detached fd");

            // --- 5. random access read-back (what ZipSubfileReader needs) --------
            long mid = PAYLOAD / 2;
            Os.lseek(fd.jfd, mid, OsConstants.SEEK_SET);
            byte[] chunk = new byte[64];
            int got = Os.read(fd.jfd, chunk, 0, chunk.length);
            boolean match = true;
            for (int k = 0; k < got; k++) if (chunk[k] != payload[(int) mid + k]) match = false;
            say((match && got == 64 ? "PASS" : "FAIL") + " 5 random-access read at "
                    + mid + " got=" + got + " match=" + match);

            // --- 6. /proc/self/fd/N as a real path (would unblock native libs) ---
            String procPath = "/proc/self/fd/" + fd.raw;
            try (RandomAccessFile raf = new RandomAccessFile(procPath, "r")) {
                raf.seek(mid);
                byte[] c2 = new byte[64];
                raf.readFully(c2);
                boolean m2 = true;
                for (int k = 0; k < 64; k++) if (c2[k] != payload[(int) mid + k]) m2 = false;
                say((m2 ? "PASS" : "FAIL") + " 6 /proc/self/fd path seek+read match=" + m2);
            } catch (Throwable t) {
                say("FAIL 6 /proc/self/fd path unusable: " + t.getClass().getSimpleName()
                        + " " + t.getMessage());
            }
        } finally {
            fd.close();
        }

        // --- 7. reopen by URI and verify durability ------------------------------
        Fd rfd = detach(doc, "r");
        try {
            StructStat st2 = Os.fstat(rfd.jfd);
            byte[] all = new byte[PAYLOAD];
            int n = 0;
            while (n < PAYLOAD) {
                int r = Os.read(rfd.jfd, all, n, PAYLOAD - n);
                if (r <= 0) break;
                n += r;
            }
            boolean same = n == PAYLOAD;
            if (same) for (int k = 0; k < PAYLOAD; k++) if (all[k] != payload[k]) { same = false; break; }
            say((same ? "PASS" : "FAIL") + " 7 reopen 'r' size=" + st2.st_size
                    + " read=" + n + " identical=" + same);
        } finally { if (rfd != null) rfd.close(); }

        // --- 8. 'rwt' truncate mode ----------------------------------------------
        try {
            Fd tfd = detach(doc, "rwt");
            if (tfd == null) { say("FAIL 8 rwt unsupported"); }
            else {
                StructStat st3 = Os.fstat(tfd.jfd);
                say("PASS 8 rwt accepted, size after truncate = " + st3.st_size);
                tfd.close();
            }
        } catch (Throwable t) {
            say("FAIL 8 rwt rejected: " + t.getClass().getSimpleName() + " " + t.getMessage());
        }

        // --- 9. rename round trip (the commit sequence depends on this) ----------
        try {
            Uri renamed = DocumentsContract.renameDocument(
                    getContentResolver(), doc, "obfdprobe.tilt.ob-bak");
            say("PASS 9 renameDocument -> " + (renamed == null ? "same uri (null)" : renamed.getLastPathSegment()));
            Uri target = renamed != null ? renamed : doc;
            // rename back so cleanup is predictable
            Uri back = DocumentsContract.renameDocument(getContentResolver(), target, "obfdprobe.tilt");
            doc = back != null ? back : target;
            say("PASS 9b renamed back");
        } catch (Throwable t) {
            say("FAIL 9 rename: " + t.getClass().getSimpleName() + " " + t.getMessage());
        }

        // --- 10. rename-over-existing (does it clobber or fail?) -----------------
        try {
            Uri other = DocumentsContract.createDocument(
                    getContentResolver(), dir, "application/octet-stream", "obfdprobe-2.tilt");
            Uri clash = DocumentsContract.renameDocument(getContentResolver(), other, "obfdprobe.tilt");
            String nm = clash == null ? "(null)" : displayName(clash);
            say("INFO 10 rename onto existing name -> " + nm
                    + "  (deduped rather than clobbered = safe)");
            if (clash != null) DocumentsContract.deleteDocument(getContentResolver(), clash);
            else DocumentsContract.deleteDocument(getContentResolver(), other);
        } catch (Throwable t) {
            say("INFO 10 rename onto existing threw: " + t.getClass().getSimpleName()
                    + " (also safe)");
        }

        // --- 11. cleanup ----------------------------------------------------------
        boolean del = DocumentsContract.deleteDocument(getContentResolver(), doc);
        say((del ? "PASS" : "FAIL") + " 11 deleteDocument");
        say("--- probe complete ---");
    }

    /** A detached descriptor: the raw int we own, adopted so android.system.Os can use it. */
    private static final class Fd {
        int raw;
        ParcelFileDescriptor pfd;
        FileDescriptor jfd;
        void close() { try { pfd.close(); } catch (Throwable ignored) { } }
    }

    private Fd detach(Uri uri, String mode) throws Exception {
        ParcelFileDescriptor p = getContentResolver().openFileDescriptor(uri, mode);
        if (p == null) return null;
        int raw = p.detachFd();
        p.close();
        Fd f = new Fd();
        f.raw = raw;
        f.pfd = ParcelFileDescriptor.adoptFd(raw);
        f.jfd = f.pfd.getFileDescriptor();
        return f;
    }

    private int queryFlags(Uri doc) {
        try (Cursor c = getContentResolver().query(doc,
                new String[]{DocumentsContract.Document.COLUMN_FLAGS}, null, null, null)) {
            if (c != null && c.moveToFirst()) return c.getInt(0);
        } catch (Throwable ignored) { }
        return 0;
    }

    private String displayName(Uri doc) {
        try (Cursor c = getContentResolver().query(doc,
                new String[]{DocumentsContract.Document.COLUMN_DISPLAY_NAME}, null, null, null)) {
            if (c != null && c.moveToFirst()) return c.getString(0);
        } catch (Throwable ignored) { }
        return "?";
    }

    private static boolean has(int flags, int bit) { return (flags & bit) != 0; }

    private void say(String s) {
        Log.i(TAG, s);
        mLog.append(s).append('\n');
        runOnUiThread(() -> mOut.setText(mLog.toString()));
    }
}
