use std::env;

fn main() {
    let directory = env::var("LRPA_NATIVE_DIR")
        .expect("LRPA_NATIVE_DIR must point to the directory containing LRPA.Native.lib");
    println!("cargo:rustc-link-search=native={directory}");
    println!("cargo:rustc-link-lib=dylib=LRPA.Native");
    println!("cargo:rerun-if-env-changed=LRPA_NATIVE_DIR");
}
