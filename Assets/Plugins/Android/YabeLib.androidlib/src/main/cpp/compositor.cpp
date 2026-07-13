#include <android/log.h>
#include <jni.h>
#include <sys/socket.h>
#include <unistd.h>
#include <yabe-compositor.h>

#include <thread>

#define LOG_TAG "YabeXreal"
#define LOG_DBG(...)                                                           \
  __android_log_print(ANDROID_LOG_DEBUG, LOG_TAG, __VA_ARGS__)
#define LOG_ERR(...)                                                           \
  __android_log_print(ANDROID_LOG_ERROR, LOG_TAG, __VA_ARGS__)

namespace {
std::thread compositor_thread;
}

extern "C" {
JNIEXPORT void JNICALL Java_org_yabe_xreal_CompositorService_startCompositor(
    JNIEnv* env, jobject obj, jint fd) {
  compositor_thread = std::thread([fd]() {
    LOG_DBG("compositor start with received fd = %d", fd);
    yabe_compositor_start(
        [](const char* s) {
          __android_log_print(ANDROID_LOG_DEBUG, "YabeCompositor", "%s", s);
        },
        fd);
    LOG_DBG("finished: yabe_compositor_start()");
  });
}
JNIEXPORT void JNICALL
Java_org_yabe_xreal_CompositorService_terminateCompositor() {
  if (compositor_thread.joinable()) {
    LOG_DBG("terminating compositor thread");
    yabe_compositor_terminate();
    compositor_thread.join();
    LOG_DBG("compositor thread terminated");
  } else {
    LOG_DBG("compositor thread is not running");
  }
}
}
