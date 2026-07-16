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
bool        is_compositor_running_ = false;
}  // namespace

extern "C" {
bool is_compositor_running() {
  return is_compositor_running_;
}

void terminate_compositor() {
  if (compositor_thread.joinable()) {
    LOG_DBG("terminating compositor thread");
    yabe_compositor_terminate();
    compositor_thread.join();
    is_compositor_running_ = false;
    LOG_DBG("compositor thread terminated");
  } else {
    LOG_DBG("compositor thread is not running");
  }
}

JNIEXPORT void JNICALL Java_org_yabe_xreal_CompositorService_startCompositor(
    JNIEnv* env, jobject obj) {
  compositor_thread      = std::thread([]() {
    yabe_compositor_init([](const char* s) {
      __android_log_print(ANDROID_LOG_DEBUG, "YabeCompositor", "%s", s);
    });
    LOG_DBG("compositor start");
    yabe_compositor_start();
    LOG_DBG("finished: yabe_compositor_start()");
  });
  is_compositor_running_ = true;
}

JNIEXPORT void JNICALL Java_org_yabe_xreal_CompositorService_addClientFd(
    JNIEnv* env, jobject obj, jint fd) {
  yabe_compositor_add_client_fd(fd);
}

JNIEXPORT void JNICALL
Java_org_yabe_xreal_CompositorService_terminateCompositor() {
  terminate_compositor();
}

JNIEXPORT jboolean JNICALL
Java_org_yabe_xreal_CompositorService_isCompositorRunning(
    JNIEnv* env, jobject obj) {
  return is_compositor_running_;
}
}
