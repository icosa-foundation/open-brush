// Copyright 2026 The Open Brush Authors. Licensed under the Apache License, Version 2.0.
package foundation.icosa.openbrush;

import android.app.Activity;
import android.content.Intent;
import android.graphics.Color;
import android.graphics.drawable.Drawable;
import android.os.Bundle;
import android.view.Gravity;
import android.view.ViewGroup;
import android.widget.FrameLayout;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.widget.TextView;

/** Keeps responsive Open Brush UI visible while Unity's XR session is unavailable. */
public final class StorageLoadingActivity extends Activity {
    private static final Object LOCK = new Object();
    private static StorageLoadingActivity s_Current;
    private static boolean s_FinishRequested;

    public static void show(Activity parent) {
        synchronized (LOCK) { s_FinishRequested = false; }
        parent.startActivity(new Intent(parent, StorageLoadingActivity.class));
    }

    public static void hide() {
        StorageLoadingActivity current;
        synchronized (LOCK) {
            current = s_Current;
            if (current == null) s_FinishRequested = true;
        }
        if (current != null) current.runOnUiThread(current::finish);
    }

    @Override protected void onCreate(Bundle state) {
        super.onCreate(state);
        boolean finish;
        synchronized (LOCK) {
            s_Current = this;
            finish = s_FinishRequested;
        }
        if (finish) {
            finish();
            return;
        }

        FrameLayout root = new FrameLayout(this);
        root.setBackgroundColor(Color.rgb(10, 17, 24));

        LinearLayout content = new LinearLayout(this);
        content.setOrientation(LinearLayout.VERTICAL);
        content.setGravity(Gravity.CENTER_HORIZONTAL);
        int padding = dp(32);
        content.setPadding(padding, padding, padding, padding);

        Drawable icon = getApplicationInfo().loadIcon(getPackageManager());
        ImageView image = new ImageView(this);
        image.setImageDrawable(icon);
        content.addView(image, new LinearLayout.LayoutParams(dp(112), dp(112)));

        TextView title = new TextView(this);
        title.setText("Open Brush");
        title.setTextColor(Color.WHITE);
        title.setTextSize(30);
        title.setGravity(Gravity.CENTER);
        LinearLayout.LayoutParams titleLayout = new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        titleLayout.topMargin = dp(20);
        content.addView(title, titleLayout);

        ProgressBar progress = new ProgressBar(this);
        progress.setIndeterminate(true);
        LinearLayout.LayoutParams progressLayout = new LinearLayout.LayoutParams(dp(56), dp(56));
        progressLayout.topMargin = dp(28);
        content.addView(progress, progressLayout);

        TextView message = new TextView(this);
        message.setText("Preparing your folder...");
        message.setTextColor(Color.rgb(210, 220, 228));
        message.setTextSize(20);
        message.setGravity(Gravity.CENTER);
        LinearLayout.LayoutParams messageLayout = new LinearLayout.LayoutParams(
            ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        messageLayout.topMargin = dp(16);
        content.addView(message, messageLayout);

        FrameLayout.LayoutParams contentLayout = new FrameLayout.LayoutParams(
            ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT, Gravity.CENTER);
        root.addView(content, contentLayout);
        setContentView(root);
    }

    @Override protected void onDestroy() {
        synchronized (LOCK) {
            if (s_Current == this) s_Current = null;
        }
        super.onDestroy();
    }

    @Override public void onBackPressed() {
        // Folder preparation must finish before Unity can safely continue startup.
    }

    private int dp(int value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }
}
