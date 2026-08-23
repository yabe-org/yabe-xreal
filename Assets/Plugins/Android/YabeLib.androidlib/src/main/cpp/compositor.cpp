#include <android/log.h>
#include <jni.h>
#include <sys/socket.h>
#include <unistd.h>
#include <yabe-compositor.h>

#include <atomic>
#include <string>
#include <thread>

#define LOG_TAG "YabeXreal"
#define LOG_DBG(...)                                                           \
  __android_log_print(ANDROID_LOG_DEBUG, LOG_TAG, __VA_ARGS__)
#define LOG_ERR(...)                                                           \
  __android_log_print(ANDROID_LOG_ERROR, LOG_TAG, __VA_ARGS__)

namespace {
std::thread      compositor_thread;
std::atomic_bool is_compositor_running_     = false;
std::atomic_bool is_compositor_initialized_ = false;
JavaVM*          java_vm_                   = nullptr;
jobject          compositor_service_        = nullptr;
std::string      keymap_str_;

int exec_in_andlify(const char* const * argv) {
  JNIEnv* env                     = nullptr;
  bool    attached_current_thread = false;
  if (java_vm_->GetEnv(reinterpret_cast<void**>(&env), JNI_VERSION_1_6) ==
      JNI_EDETACHED) {
    if (java_vm_->AttachCurrentThread(&env, nullptr) != JNI_OK) {
      LOG_ERR("Failed to attach compositor thread to JVM");
      return -1;
    }
    attached_current_thread = true;
  }

  auto* service_class = env->GetObjectClass(compositor_service_);
  auto  exec_method =
      env->GetMethodID(service_class, "exec", "([Ljava/lang/String;)I");
  if (!exec_method) {
    env->ExceptionClear();
    env->DeleteLocalRef(service_class);
    if (attached_current_thread) {
      java_vm_->DetachCurrentThread();
    }
    LOG_ERR("Failed to find CompositorService.exec()");
    return -1;
  }

  size_t argc = 0;
  while (argv[argc]) {
    ++argc;
  }
  auto* string_class = env->FindClass("java/lang/String");
  auto* args         = env->NewObjectArray(argc, string_class, nullptr);
  for (size_t i = 0; i < argc; ++i) {
    auto* arg = env->NewStringUTF(argv[i]);
    env->SetObjectArrayElement(args, i, arg);
    env->DeleteLocalRef(arg);
  }
  const auto result =
      env->CallIntMethod(compositor_service_, exec_method, args);
  env->DeleteLocalRef(args);
  env->DeleteLocalRef(string_class);
  env->DeleteLocalRef(service_class);

  if (env->ExceptionCheck()) {
    env->ExceptionDescribe();
    env->ExceptionClear();
    LOG_ERR("Failed to execute command in xreal-client");
    if (attached_current_thread) {
      java_vm_->DetachCurrentThread();
    }
    return -1;
  }
  if (attached_current_thread) {
    java_vm_->DetachCurrentThread();
  }
  return result;
}

const char* create_keymap(const char* rules, const char* model,
    const char* layout, const char* variant, const char* options) {
  keymap_str_.clear();
  JNIEnv* env                     = nullptr;
  bool    attached_current_thread = false;
  if (java_vm_->GetEnv(reinterpret_cast<void**>(&env), JNI_VERSION_1_6) ==
      JNI_EDETACHED) {
    if (java_vm_->AttachCurrentThread(&env, nullptr) != JNI_OK) {
      LOG_ERR("Failed to attach compositor thread to JVM");
      return nullptr;
    }
    attached_current_thread = true;
  }

  auto* service_class        = env->GetObjectClass(compositor_service_);
  auto  create_keymap_method = env->GetMethodID(service_class, "createKeymap",
      "(Ljava/lang/String;Ljava/lang/String;Ljava/lang/String;Ljava/lang/"
      "String;Ljava/lang/String;)Ljava/lang/String;");
  if (!create_keymap_method) {
    env->ExceptionClear();
    env->DeleteLocalRef(service_class);
    if (attached_current_thread) {
      java_vm_->DetachCurrentThread();
    }
    LOG_ERR("Failed to find CompositorService.createKeymap()");
    return nullptr;
  }
  auto* rules_string   = env->NewStringUTF(rules);
  auto* model_string   = env->NewStringUTF(model);
  auto* layout_string  = env->NewStringUTF(layout);
  auto* variant_string = env->NewStringUTF(variant);
  auto* options_string = env->NewStringUTF(options);
  auto* result = static_cast<jstring>(env->CallObjectMethod(compositor_service_,
      create_keymap_method, rules_string, model_string, layout_string,
      variant_string, options_string));
  env->DeleteLocalRef(rules_string);
  env->DeleteLocalRef(model_string);
  env->DeleteLocalRef(layout_string);
  env->DeleteLocalRef(variant_string);
  env->DeleteLocalRef(options_string);
  env->DeleteLocalRef(service_class);

  if (env->ExceptionCheck()) {
    env->ExceptionDescribe();
    env->ExceptionClear();
    LOG_ERR("Failed to request xkb keymap from xreal-client");
  } else if (result) {
    const char* result_chars = env->GetStringUTFChars(result, nullptr);
    if (result_chars) {
      keymap_str_ = result_chars;
      env->ReleaseStringUTFChars(result, result_chars);
    }
    env->DeleteLocalRef(result);
  }
  if (attached_current_thread) {
    java_vm_->DetachCurrentThread();
  }
  return keymap_str_.empty() ? nullptr : keymap_str_.c_str();
}
}  // namespace

extern "C" {
bool is_compositor_running() {
  return is_compositor_running_.load();
}

void terminate_compositor() {
  if (compositor_thread.joinable()) {
    LOG_DBG("terminating compositor thread");
    yabe_compositor_terminate();
    compositor_thread.join();
    is_compositor_running_.store(false);
    LOG_DBG("compositor thread terminated");
  } else {
    LOG_DBG("compositor thread is not running");
  }
}

JNIEXPORT void JNICALL Java_org_yabe_xreal_CompositorService_startCompositor(
    JNIEnv* env, jobject obj, jstring config_text) {
  env->GetJavaVM(&java_vm_);
  compositor_service_      = env->NewGlobalRef(obj);
  const char* config_chars = env->GetStringUTFChars(config_text, nullptr);
  std::string config(config_chars);
  env->ReleaseStringUTFChars(config_text, config_chars);
  is_compositor_running_.store(true);
  compositor_thread = std::thread([config = std::move(config)]() {
    yabe_compositor_init(
        [](const char* s) {
          __android_log_print(ANDROID_LOG_DEBUG, "YabeCompositor", "%s", s);
        },
        create_keymap, exec_in_andlify);
    yabe_compositor_load_config(config.c_str());
    is_compositor_initialized_.store(true);
    LOG_DBG("compositor start");
    yabe_compositor_start();
    is_compositor_initialized_.store(false);
    LOG_DBG("finished: yabe_compositor_start()");
  });
}

JNIEXPORT void JNICALL Java_org_yabe_xreal_CompositorService_addClientFd(
    JNIEnv* env, jobject obj, jint fd) {
  yabe_compositor_add_client_fd(fd);
}

JNIEXPORT void JNICALL
Java_org_yabe_xreal_CompositorService_terminateCompositor(
    JNIEnv* env, jobject obj) {
  terminate_compositor();
  if (compositor_service_) {
    env->DeleteGlobalRef(compositor_service_);
    compositor_service_ = nullptr;
  }
  keymap_str_.clear();
}

JNIEXPORT jboolean JNICALL
Java_org_yabe_xreal_CompositorService_isCompositorRunning(
    JNIEnv* env, jobject obj) {
  return is_compositor_running_.load();
}

void send_keyboard_key(uint32_t key, bool is_pressed) {
  if (!is_compositor_initialized_.load()) {
    return;
  }
  yabe_compositor_key(key, is_pressed);
}
}
