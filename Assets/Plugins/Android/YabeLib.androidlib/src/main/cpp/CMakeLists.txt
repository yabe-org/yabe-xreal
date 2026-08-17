cmake_minimum_required(VERSION 3.22)
project(compositor)

set(CMAKE_EXPORT_COMPILE_COMMANDS ON)
set(CMAKE_COLOR_DIAGNOSTICS ON)
set(CMAKE_CXX_STANDARD 23)

add_library(${PROJECT_NAME} SHARED compositor.cpp)
target_link_libraries(${PROJECT_NAME}
  log
  GLESv3
  /home/watasuke/program/yabe/compositor/android-build/libyabe_compositor.a
)
target_include_directories(${PROJECT_NAME} PUBLIC
  /home/watasuke/program/yabe/compositor
  ${CMAKE_CURRENT_BINARY_DIR}
)
