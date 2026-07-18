cmake_minimum_required(VERSION 3.22)
project(compositor)

set(CMAKE_EXPORT_COMPILE_COMMANDS ON)
set(CMAKE_COLOR_DIAGNOSTICS ON)
set(CMAKE_CXX_STANDARD 23)

include(FetchContent)
FetchContent_Declare(
  xkbcommon_src
  GIT_REPOSITORY https://github.com/xkbcommon/libxkbcommon.git
  GIT_TAG xkbcommon-1.13.2
  GIT_SHALLOW true
)
FetchContent_GetProperties(xkbcommon_src)
if(NOT xkbcommon_src_POPULATED)
  FetchContent_Populate(xkbcommon_src)
endif()

configure_file("${CMAKE_CURRENT_LIST_DIR}/meson_cross.txt.in" ${CMAKE_CURRENT_BINARY_DIR}/meson_cross.txt)
include(ExternalProject)
ExternalProject_Add(
  xkbcommon_project
  SOURCE_DIR ${xkbcommon_src_SOURCE_DIR}
  BUILD_ALWAYS 0
  CONFIGURE_COMMAND meson setup <BINARY_DIR> <SOURCE_DIR> --cross-file ${CMAKE_CURRENT_BINARY_DIR}/meson_cross.txt --default-library=static -Denable-tools=false -Denable-x11=false -Denable-docs=false -Denable-wayland=false -Denable-xkbregistry=false -Denable-bash-completion=false
  BUILD_COMMAND meson compile -C <BINARY_DIR>
  INSTALL_COMMAND ""
  BUILD_BYPRODUCTS <BINARY_DIR>/libxkbcommon.a
)
ExternalProject_Get_Property(xkbcommon_project BINARY_DIR)
ExternalProject_Get_Property(xkbcommon_project SOURCE_DIR)
add_library(xkbcommon STATIC IMPORTED)
set_target_properties(xkbcommon PROPERTIES
  IMPORTED_LOCATION ${BINARY_DIR}/libxkbcommon.a
  INTERFACE_INCLUDE_DIRECTORIES "$<BUILD_INTERFACE:${SOURCE_DIR}/include>"
)
add_dependencies(xkbcommon xkbcommon_project)

find_program(XKBCLI xkbcli REQUIRED)
execute_process(
  COMMAND ${XKBCLI} compile-keymap --layout us
  OUTPUT_VARIABLE DEFAULT_KEYMAP
  RESULT_VARIABLE XKBCLI_RESULT
)
if(NOT XKBCLI_RESULT EQUAL 0)
  message(FATAL_ERROR "Failed to compile the default keymap")
endif()
configure_file(default-keymap.hpp.in ${CMAKE_CURRENT_BINARY_DIR}/default-keymap.hpp @ONLY)

add_library(${PROJECT_NAME} SHARED
  compositor.cpp
  keyboard.cpp
)
add_dependencies(${PROJECT_NAME} xkbcommon_project)
target_link_libraries(${PROJECT_NAME}
  log
  GLESv3
  xkbcommon
  /home/watasuke/program/yabe/compositor/android-build/libyabe_compositor.a
)
target_include_directories(${PROJECT_NAME} PUBLIC
  /home/watasuke/program/yabe/compositor
  ${CMAKE_CURRENT_BINARY_DIR}
)
