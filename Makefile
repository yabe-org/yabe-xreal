TARGET           := ./yabe-xreal.apk
UNITY_VERSION    ?= 6000.0.77f1
UNITY_EXECUTABLE ?= $(HOME)/Unity/Hub/Editor/$(UNITY_VERSION)/Editor/Unity
XREAL_SDK        := Packages/com.xreal.xr.tgz

.PHONY: all apk $(TARGET) install unity log

all: $(TARGET) install

apk: $(TARGET)
$(TARGET): $(XREAL_SDK)
	export UNITY_VERSION=$(UNITY_VERSION) && \
	$(UNITY_EXECUTABLE) -quit -batchmode -nographics \
		-projectPath . \
		-activeBuildProfile "Assets/Settings/Build Profiles/Android.asset" \
		-build $(TARGET)

# do not set TARGET as a dependency to avoid building
install:
	adb install $(TARGET)

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

