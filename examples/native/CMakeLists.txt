cmake_minimum_required(VERSION 3.20)
project(lrpa_native_examples LANGUAGES C CXX)

if(NOT DEFINED LRPA_NATIVE_DIR)
    message(FATAL_ERROR "Set LRPA_NATIVE_DIR to the NativeAOT publish directory")
endif()

add_executable(lrpa_c ../c/main.c)
target_include_directories(lrpa_c PRIVATE ../../native/include)
target_link_libraries(lrpa_c PRIVATE "${LRPA_NATIVE_DIR}/LRPA.Native.lib")

add_executable(lrpa_cpp ../cpp/main.cpp)
target_compile_features(lrpa_cpp PRIVATE cxx_std_17)
target_include_directories(lrpa_cpp PRIVATE ../../native/include)
target_link_libraries(lrpa_cpp PRIVATE "${LRPA_NATIVE_DIR}/LRPA.Native.lib")

add_executable(lrpa_abi_smoke ../../tests/native/abi_smoke.c)
target_include_directories(lrpa_abi_smoke PRIVATE ../../native/include)
target_link_libraries(lrpa_abi_smoke PRIVATE "${LRPA_NATIVE_DIR}/LRPA.Native.lib")

foreach(target lrpa_c lrpa_cpp lrpa_abi_smoke)
    add_custom_command(TARGET ${target} POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_if_different
            "${LRPA_NATIVE_DIR}/LRPA.Native.dll"
            "$<TARGET_FILE_DIR:${target}>")
endforeach()

enable_testing()
add_test(NAME lrpa_abi_smoke COMMAND lrpa_abi_smoke)
