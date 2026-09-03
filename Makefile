DEBUG_TARGET     := ./yabe-xreal-debug.apk
RELEASE_TARGET   := ./yabe-xreal-release.apk
DEBUG_PROFILE    := Assets/Settings/Build Profiles/AndroidDevelopment.asset
RELEASE_PROFILE  := Assets/Settings/Build Profiles/Android.asset
UNITY_VERSION    ?= 6000.0.77f1
UNITY_EXECUTABLE ?= $(HOME)/Unity/Hub/Editor/$(UNITY_VERSION)/Editor/Unity
UNITY_ANDROID_NDK ?= $(HOME)/Unity/Hub/Editor/$(UNITY_VERSION)/Editor/Data/PlaybackEngines/AndroidPlayer/NDK
XREAL_SDK        := Packages/com.xreal.xr.tgz
NATIVE_SOURCE    := Assets/Plugins/Android/YabeLib.androidlib/src/main/cpp
CLANGD_BUILD     := .clangd-build

.PHONY: all apk release-apk install release-install unity log clangd

all: apk install

apk: $(XREAL_SDK)
	export UNITY_VERSION=$(UNITY_VERSION) && \
	$(UNITY_EXECUTABLE) -quit -batchmode -nographics \
		-projectPath . \
		-activeBuildProfile "$(DEBUG_PROFILE)" \
		-build $(DEBUG_TARGET)
	@echo
release-apk: $(XREAL_SDK)
	export UNITY_VERSION=$(UNITY_VERSION) && \
	$(UNITY_EXECUTABLE) -quit -batchmode -nographics \
		-projectPath . \
		-activeBuildProfile "$(RELEASE_PROFILE)" \
		-build $(RELEASE_TARGET)
	@echo # after build. shell does not accept input, so reflesh it by this

# do not set TARGET as a dependency to avoid building
install:
	adb install $(DEBUG_TARGET)
release-install:
	adb install $(RELEASE_TARGET)

$(XREAL_SDK):
	$(error "$(XREAL_SDK) is not found")

unity:
	$(UNITY_EXECUTABLE) -force-gfx-mt -projectPath . -openfile Assets/Scene.unity

log:
	adb logcat -c && clear && adb logcat -s DEBUG YabeXreal YabeCompositor
logger:
	adb logcat -c && clear && adb logcat -s DEBUG YabeXreal YabeCompositor Unity XREAL
loggest:
	adb logcat -c && clear && adb logcat

clangd:
	cmake -S $(NATIVE_SOURCE) -B $(CLANGD_BUILD) \
		-DCMAKE_TOOLCHAIN_FILE=$(UNITY_ANDROID_NDK)/build/cmake/android.toolchain.cmake \
		-DANDROID_ABI=arm64-v8a \
		-DANDROID_PLATFORM=android-31 \
		-DCMAKE_BUILD_TYPE=Debug
