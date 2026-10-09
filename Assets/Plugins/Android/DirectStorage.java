// Copyright 2026 The Open Brush Authors. Licensed under the Apache License, Version 2.0.
package foundation.icosa.openbrush;

import android.app.Activity;
import android.app.AlertDialog;
import android.app.Fragment;
import android.content.Context;
import android.content.Intent;
import android.content.UriPermission;
import android.database.Cursor;
import android.net.Uri;
import android.os.Bundle;
import android.os.Environment;
import android.provider.DocumentsContract;
import android.provider.MediaStore;
import com.unity3d.player.UnityPlayer;
import java.io.File;
import java.io.FileInputStream;
import java.io.IOException;
import java.util.ArrayDeque;
import java.util.HashSet;
import java.util.Locale;
import org.json.JSONArray;
import org.json.JSONObject;

public final class DirectStorage {
    private static final String AUTHORITY = "com.android.externalstorage.documents";

    public static String documentsRoot(String name) throws IOException {
        if (!validName(name)) throw new IOException("Invalid workspace folder name");
        return new File(Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOCUMENTS),
            name).getCanonicalPath();
    }

    private static boolean validName(String name) {
        return name != null && !name.isEmpty() && !name.equals(".") && !name.equals("..")
            && name.indexOf('/') < 0 && name.indexOf('\\') < 0 && name.indexOf('\0') < 0;
    }

    private static String rootId(String name) throws IOException {
        String base = Environment.getExternalStorageDirectory().getCanonicalPath() + "/";
        String root = documentsRoot(name);
        if (!root.startsWith(base)) throw new IOException("Workspace is not on primary shared storage");
        return "primary:" + root.substring(base.length());
    }

    private static boolean matches(Uri tree, String name) throws IOException {
        return AUTHORITY.equals(tree.getAuthority()) && DocumentsContract.isTreeUri(tree)
            && rootId(name).equals(DocumentsContract.getTreeDocumentId(tree));
    }

    public static void begin(String name, String receiver) {
        Activity activity = UnityPlayer.currentActivity;
        if (activity == null) { send(receiver, "OnStorageError", "Android activity is unavailable"); return; }
        try {
            File root = new File(documentsRoot(name));
            if (!root.isDirectory() && !root.mkdirs()) throw new IOException("Cannot create Documents workspace");
            for (UriPermission grant : activity.getContentResolver().getPersistedUriPermissions()) {
                if (grant.isReadPermission() && grant.isWritePermission() && matches(grant.getUri(), name)) {
                    prepare(activity.getApplicationContext(), grant.getUri(), name, receiver);
                    return;
                }
            }
            activity.runOnUiThread(() -> {
                try {
                    if (activity.isFinishing() || activity.isDestroyed()) {
                        send(receiver, "OnStorageCanceled", "");
                        return;
                    }
                    Picker picker = new Picker();
                    Bundle args = new Bundle();
                    args.putString("name", name);
                    args.putString("receiver", receiver);
                    picker.setArguments(args);
                    activity.getFragmentManager().beginTransaction().add(picker, "OBDS_Picker").commit();
                    activity.getFragmentManager().executePendingTransactions();
                    picker.launch();
                } catch (Exception error) { send(receiver, "OnStorageError", error.toString()); }
            });
        } catch (Exception error) { send(receiver, "OnStorageError", error.toString()); }
    }

    public static void showRetry(String name, String receiver) {
        Activity activity = UnityPlayer.currentActivity;
        if (activity == null) { send(receiver, "OnStorageCanceled", ""); return; }
        activity.runOnUiThread(() -> {
            try {
                if (activity.isFinishing() || activity.isDestroyed()) {
                    send(receiver, "OnStorageCanceled", "");
                    return;
                }
                new AlertDialog.Builder(activity)
            .setTitle("Connect your Open Brush folder")
            .setMessage("Choose the workspace folder in Documents so Open Brush can access existing files. Cancel to exit and connect it later.")
            .setPositiveButton("Try again", (dialog, which) -> begin(name, receiver))
            .setNegativeButton("Cancel", (dialog, which) -> send(receiver, "OnStorageCanceled", ""))
            .setOnCancelListener(dialog -> send(receiver, "OnStorageCanceled", ""))
                    .show();
            } catch (Exception error) { send(receiver, "OnStorageCanceled", ""); }
        });
    }

    public static final class Picker extends Fragment {
        public void launch() {
            try {
                Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT_TREE);
                intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_GRANT_WRITE_URI_PERMISSION
                    | Intent.FLAG_GRANT_PERSISTABLE_URI_PERMISSION);
                intent.putExtra(DocumentsContract.EXTRA_INITIAL_URI,
                    DocumentsContract.buildDocumentUri(AUTHORITY, rootId(getArguments().getString("name"))));
                startActivityForResult(intent, 7420);
            } catch (Exception error) {
                send(getArguments().getString("receiver"), "OnStorageError", error.toString());
                remove();
            }
        }

        @Override public void onActivityResult(int request, int result, Intent data) {
            if (request != 7420) return;
            String receiver = getArguments().getString("receiver");
            String name = getArguments().getString("name");
            try {
                if (result != Activity.RESULT_OK || data == null || data.getData() == null) {
                    send(receiver, "OnStorageCanceled", "");
                    return;
                }
                Uri tree = data.getData();
                if (!matches(tree, name)) throw new IOException("Choose the workspace folder in Documents");
                int flags = data.getFlags() & (Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_GRANT_WRITE_URI_PERMISSION);
                if (flags != (Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_GRANT_WRITE_URI_PERMISSION))
                    throw new IOException("The workspace requires read and write access");
                getActivity().getContentResolver().takePersistableUriPermission(tree, flags);
                prepare(getActivity().getApplicationContext(), tree, name, receiver);
            } catch (Exception error) { send(receiver, "OnStorageError", error.toString()); }
            finally { remove(); }
        }

        private void remove() {
            if (getFragmentManager() != null)
                getFragmentManager().beginTransaction().remove(this).commitAllowingStateLoss();
        }
    }

    private static void prepare(Context context, Uri tree, String name, String receiver) {
        new Thread(() -> {
            try {
                String root = documentsRoot(name);
                String id = rootId(name);
                ArrayDeque<String[]> pending = new ArrayDeque<>();
                pending.add(new String[] {id, ""});
                HashSet<String> paths = new HashSet<>();
                int entries = 0;
                JSONArray files = new JSONArray();
                JSONArray directories = new JSONArray();
                int failed = 0;
                String firstFailure = "";
                while (!pending.isEmpty()) {
                    String[] parent = pending.removeFirst();
                    Uri children = DocumentsContract.buildChildDocumentsUriUsingTree(tree, parent[0]);
                    try (Cursor cursor = context.getContentResolver().query(children, new String[] {
                        DocumentsContract.Document.COLUMN_DOCUMENT_ID,
                        DocumentsContract.Document.COLUMN_DISPLAY_NAME,
                        DocumentsContract.Document.COLUMN_MIME_TYPE }, null, null, null)) {
                        if (cursor == null) throw new IOException("Folder provider returned no entries");
                        while (cursor.moveToNext()) {
                            String leaf = cursor.getString(1);
                            if (!validName(leaf)) throw new IOException("Invalid provider filename");
                            String relative = parent[1].isEmpty() ? leaf : parent[1] + "/" + leaf;
                            if (!paths.add(relative.toLowerCase(Locale.ROOT))) throw new IOException("Ambiguous workspace filenames");
                            String childId = cursor.getString(0);
                            if (!childId.equals(id + "/" + relative)) throw new IOException("Document escapes workspace root");
                            boolean directory = DocumentsContract.Document.MIME_TYPE_DIR.equals(cursor.getString(2));
                            Uri document = DocumentsContract.buildDocumentUriUsingTree(tree, childId);
                            String canonical = new File(root, relative).getCanonicalPath();
                            if (!canonical.startsWith(root + "/")) throw new IOException("File escapes workspace root");
                            if (directory) {
                                pending.add(new String[] {childId, relative});
                                directories.put(canonical);
                            }
                            else {
                                // Translation does not establish ownership. Verify the path
                                // the existing readers actually use, without reading/copying payloads.
                                String translationFailure = "";
                                try {
                                    Uri media = MediaStore.getMediaUri(context, document);
                                    if (media == null) translationFailure = "No MediaStore URI";
                                } catch (Exception error) { translationFailure = error.toString(); }
                                try (FileInputStream stream = new FileInputStream(canonical)) { }
                                catch (Exception error) {
                                    if (failed++ == 0) firstFailure = error + "; translation: " + translationFailure;
                                }
                                files.put(canonical);
                            }
                            entries++;
                        }
                    }
                }
                JSONObject result = new JSONObject();
                result.put("root", root);
                result.put("documents", entries);
                result.put("files", files);
                result.put("directories", directories);
                result.put("failed", failed);
                result.put("error", firstFailure);
                send(receiver, "OnStoragePrepared", result.toString());
            } catch (Exception error) { send(receiver, "OnStorageError", error.toString()); }
        }, "OBDS_Prepare").start();
    }

    private static void send(String receiver, String method, String value) {
        UnityPlayer.UnitySendMessage(receiver, method, value);
    }
}
