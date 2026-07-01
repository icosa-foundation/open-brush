package foundation.icosa.openbrush.storage;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.DialogInterface;
import android.content.Intent;
import android.database.Cursor;
import android.net.Uri;
import android.os.Bundle;
import android.provider.DocumentsContract;

import com.unity3d.player.UnityPlayer;

public class OpenBrushStorageActivity extends Activity {
    private static final int REQUEST_OPEN_BRUSH_FOLDER = 4201;
    private static final String OPEN_BRUSH_FOLDER_NAME = "Open Brush";

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        showStoragePrompt();
    }

    private void showStoragePrompt() {
        new AlertDialog.Builder(this)
                .setTitle("Choose Open Brush folder")
                .setMessage("Open Brush needs a shared folder named \""
                        + OPEN_BRUSH_FOLDER_NAME
                        + "\" for sketches, exports, snapshots, videos, and media. "
                        + "You can choose Not Now and continue with reduced storage features.")
                .setPositiveButton("Choose Folder",
                        new DialogInterface.OnClickListener() {
                            @Override
                            public void onClick(DialogInterface dialog, int which) {
                                launchFolderPicker();
                            }
                        })
                .setNegativeButton("Not Now",
                        new DialogInterface.OnClickListener() {
                            @Override
                            public void onClick(DialogInterface dialog, int which) {
                                sendCanceledAndFinish();
                            }
                        })
                .setOnCancelListener(new DialogInterface.OnCancelListener() {
                    @Override
                    public void onCancel(DialogInterface dialog) {
                        sendCanceledAndFinish();
                    }
                })
                .show();
    }

    private void launchFolderPicker() {
        Intent intent = new Intent(Intent.ACTION_OPEN_DOCUMENT_TREE);
        intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);
        intent.addFlags(Intent.FLAG_GRANT_WRITE_URI_PERMISSION);
        intent.addFlags(Intent.FLAG_GRANT_PERSISTABLE_URI_PERMISSION);
        intent.addFlags(Intent.FLAG_GRANT_PREFIX_URI_PERMISSION);
        startActivityForResult(intent, REQUEST_OPEN_BRUSH_FOLDER);
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);

        if (requestCode == REQUEST_OPEN_BRUSH_FOLDER && resultCode == RESULT_OK && data != null) {
            Uri uri = data.getData();
            if (uri != null) {
                int flags = data.getFlags()
                        & (Intent.FLAG_GRANT_READ_URI_PERMISSION
                        | Intent.FLAG_GRANT_WRITE_URI_PERMISSION);
                String displayName = getTreeDisplayName(uri);
                if (!OPEN_BRUSH_FOLDER_NAME.equals(displayName)) {
                    showOpenBrushFolderRequired(displayName);
                    return;
                }

                persistSelectionAndFinish(uri, flags);
                return;
            }
        }

        sendCanceledAndFinish();
    }

    private void showOpenBrushFolderRequired(String displayName) {
        String selectedFolder = displayName != null && displayName.length() > 0
                ? "The selected folder is named \"" + displayName + "\"."
                : "Open Brush could not confirm the selected folder name.";
        new AlertDialog.Builder(this)
                .setTitle("Choose Open Brush folder")
                .setMessage(selectedFolder + " Please choose or create a folder named \""
                        + OPEN_BRUSH_FOLDER_NAME + "\".")
                .setPositiveButton("Choose Again",
                        new DialogInterface.OnClickListener() {
                            @Override
                            public void onClick(DialogInterface dialog, int which) {
                                launchFolderPicker();
                            }
                        })
                .setNegativeButton("Not Now",
                        new DialogInterface.OnClickListener() {
                            @Override
                            public void onClick(DialogInterface dialog, int which) {
                                sendCanceledAndFinish();
                            }
                        })
                .setOnCancelListener(new DialogInterface.OnCancelListener() {
                    @Override
                    public void onCancel(DialogInterface dialog) {
                        sendCanceledAndFinish();
                    }
                })
                .show();
    }

    private void persistSelectionAndFinish(Uri uri, int flags) {
        try {
            getContentResolver().takePersistableUriPermission(uri, flags);
            OpenBrushStorageBridge.saveOpenBrushFolderUri(this, uri.toString());
            UnityPlayer.UnitySendMessage(
                    "AndroidStorageManager",
                    "OnOpenBrushFolderSelected",
                    uri.toString());
            finish();
        } catch (Exception e) {
            // Treat an unusable provider grant like cancellation so Unity can keep running.
            sendCanceledAndFinish();
        }
    }

    private String getTreeDisplayName(Uri treeUri) {
        try {
            Uri root = DocumentsContract.buildDocumentUriUsingTree(
                    treeUri,
                    DocumentsContract.getTreeDocumentId(treeUri));
            try (Cursor cursor = getContentResolver().query(
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
        } catch (Exception e) {
            return "";
        }
        return "";
    }

    private void sendCanceledAndFinish() {
        UnityPlayer.UnitySendMessage(
                "AndroidStorageManager",
                "OnOpenBrushFolderCanceled",
                "");
        finish();
    }
}
