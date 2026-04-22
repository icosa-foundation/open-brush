// Copyright 2024 The Open Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#import "UnityAppController.h"

static NSString* _pendingShortcutType = nil;

extern "C" {
    // Called from Config.Awake() via DllImport("__Internal") to read which quick action launched the app.
    const char* OB_GetLaunchShortcutType() {
        if (_pendingShortcutType == nil) return "";
        return [_pendingShortcutType UTF8String];
    }
}

@interface OpenBrushAppController : UnityAppController
@end

@implementation OpenBrushAppController

IMPL_APP_CONTROLLER_SUBCLASS(OpenBrushAppController)

- (BOOL)application:(UIApplication*)application
    didFinishLaunchingWithOptions:(NSDictionary*)launchOptions {

    UIApplicationShortcutItem* item = launchOptions[UIApplicationLaunchOptionsShortcutItemKey];
    if (item) {
        _pendingShortcutType = [item.type copy];
    }

    BOOL result = [super application:application didFinishLaunchingWithOptions:launchOptions];

    // Return NO when launched via shortcut so the system doesn't also call
    // performActionForShortcutItem:completionHandler: for the same item.
    return item ? NO : result;
}

- (void)application:(UIApplication*)application
    performActionForShortcutItem:(UIApplicationShortcutItem*)shortcutItem
    completionHandler:(void (^)(BOOL))completionHandler {
    // Warm launch: app was suspended and user tapped a quick action.
    // Mode changes only take effect on next cold start, so we just store the type.
    _pendingShortcutType = [shortcutItem.type copy];
    completionHandler(YES);
}

@end
