use std::error::Error as StdError;
use std::ffi::{CStr, CString, NulError, c_char};
use std::fmt;
use std::ptr;

pub const ABI_VERSION: u32 = 1;
pub const INPUT_MOUSE: u32 = 0;
pub const MOUSEEVENTF_MOVE: u32 = 0x0001;
pub const MOUSEEVENTF_LEFTDOWN: u32 = 0x0002;
pub const MOUSEEVENTF_LEFTUP: u32 = 0x0004;
pub const MOUSEEVENTF_RIGHTDOWN: u32 = 0x0008;
pub const MOUSEEVENTF_RIGHTUP: u32 = 0x0010;
pub const MOUSEEVENTF_MIDDLEDOWN: u32 = 0x0020;
pub const MOUSEEVENTF_MIDDLEUP: u32 = 0x0040;
pub const MOUSEEVENTF_XDOWN: u32 = 0x0080;
pub const MOUSEEVENTF_XUP: u32 = 0x0100;
pub const MOUSEEVENTF_WHEEL: u32 = 0x0800;
pub const MOUSEEVENTF_HWHEEL: u32 = 0x1000;

const OPTION_REMOVE_OWNED_ON_CLOSE: u32 = 0x00000001;

#[repr(i32)]
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum ResultCode {
    Ok = 0,
    InvalidArgument = 1,
    InvalidHandle = 2,
    NotSupported = 3,
    ProtocolError = 4,
    NativeError = 5,
    InternalError = 6,
    BufferTooSmall = 7,
}

#[repr(C)]
struct RawOptions {
    struct_size: u32,
    flags: u32,
    creation_delay_ms: i32,
    reserved: u32,
    device_path_utf8: *const c_char,
    driver_path_utf8: *const c_char,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct MouseInput {
    pub dx: i32,
    pub dy: i32,
    pub mouse_data: u32,
    pub flags: u32,
    pub time: u32,
    pub extra_info: usize,
}

#[repr(C)]
#[derive(Clone, Copy)]
pub union InputData {
    pub mouse: MouseInput,
    pub reserved: [u8; 32],
}

#[repr(C)]
#[derive(Clone, Copy)]
pub struct Input {
    pub input_type: u32,
    pub data: InputData,
}

impl Input {
    pub fn mouse(dx: i32, dy: i32, flags: u32, mouse_data: u32) -> Self {
        Self {
            input_type: INPUT_MOUSE,
            data: InputData {
                mouse: MouseInput {
                    dx,
                    dy,
                    mouse_data,
                    flags,
                    time: 0,
                    extra_info: 0,
                },
            },
        }
    }

    pub fn move_relative(dx: i32, dy: i32) -> Self {
        Self::mouse(dx, dy, MOUSEEVENTF_MOVE, 0)
    }
}

#[repr(C, packed)]
#[derive(Clone, Copy, Debug)]
pub struct MouseReport {
    pub buttons: u16,
    pub x: i16,
    pub y: i16,
    pub wheel: i8,
    pub horizontal_pan: i8,
}

#[derive(Debug)]
pub struct LrpaError {
    pub code: i32,
    pub message: String,
}

impl fmt::Display for LrpaError {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(formatter, "LRPA error {}: {}", self.code, self.message)
    }
}

impl StdError for LrpaError {}

pub struct OpenOptions {
    remove_owned_on_close: bool,
    creation_delay_ms: i32,
    device_path: Option<CString>,
    driver_path: Option<CString>,
}

impl Default for OpenOptions {
    fn default() -> Self {
        Self {
            remove_owned_on_close: true,
            creation_delay_ms: 750,
            device_path: None,
            driver_path: None,
        }
    }
}

impl OpenOptions {
    pub fn remove_owned_on_close(mut self, enabled: bool) -> Self {
        self.remove_owned_on_close = enabled;
        self
    }

    pub fn creation_delay_ms(mut self, milliseconds: i32) -> Self {
        self.creation_delay_ms = milliseconds;
        self
    }

    pub fn device_path(mut self, path: &str) -> Result<Self, NulError> {
        self.device_path = Some(CString::new(path)?);
        Ok(self)
    }

    pub fn driver_path(mut self, path: &str) -> Result<Self, NulError> {
        self.driver_path = Some(CString::new(path)?);
        Ok(self)
    }

    fn raw(&self) -> RawOptions {
        RawOptions {
            struct_size: size_of::<RawOptions>() as u32,
            flags: if self.remove_owned_on_close {
                OPTION_REMOVE_OWNED_ON_CLOSE
            } else {
                0
            },
            creation_delay_ms: self.creation_delay_ms,
            reserved: 0,
            device_path_utf8: self
                .device_path
                .as_ref()
                .map_or(ptr::null(), |v| v.as_ptr()),
            driver_path_utf8: self
                .driver_path
                .as_ref()
                .map_or(ptr::null(), |v| v.as_ptr()),
        }
    }
}

pub struct Controller {
    handle: usize,
    created: bool,
}

impl Controller {
    pub fn open() -> Result<Self, LrpaError> {
        Self::open_with(&OpenOptions::default())
    }

    pub fn open_with(options: &OpenOptions) -> Result<Self, LrpaError> {
        validate_abi()?;
        let raw = options.raw();
        let mut handle = 0usize;
        let mut created = 0i32;
        let result = unsafe { lrpa_open(&raw, &mut handle, &mut created) };
        check(result)?;
        Ok(Self {
            handle,
            created: created != 0,
        })
    }

    pub fn created(&self) -> bool {
        self.created
    }

    pub fn move_relative(&self, x: i32, y: i32) -> Result<(), LrpaError> {
        check(unsafe { lrpa_move(self.handle, x, y) })
    }

    pub fn send_input(&self, inputs: &[Input]) -> Result<u32, LrpaError> {
        let mut sent = 0u32;
        let result = unsafe {
            lrpa_send_input(
                self.handle,
                inputs.len().try_into().map_err(|_| LrpaError {
                    code: ResultCode::InvalidArgument as i32,
                    message: "input slice is too large".into(),
                })?,
                inputs.as_ptr(),
                size_of::<Input>(),
                &mut sent,
            )
        };
        check(result)?;
        Ok(sent)
    }

    pub fn submit_report(&self, report: &MouseReport) -> Result<(), LrpaError> {
        check(unsafe { lrpa_submit_report(self.handle, report) })
    }
}

impl Drop for Controller {
    fn drop(&mut self) {
        if self.handle != 0 {
            unsafe {
                let _ = lrpa_close(self.handle);
            }
            self.handle = 0;
        }
    }
}

fn validate_abi() -> Result<(), LrpaError> {
    let version = unsafe { lrpa_abi_version() };
    let sizes_match = unsafe {
        lrpa_options_size() == size_of::<RawOptions>()
            && lrpa_input_size() == size_of::<Input>()
            && lrpa_mouse_report_size() == size_of::<MouseReport>()
    };
    if version == ABI_VERSION && sizes_match {
        Ok(())
    } else {
        Err(LrpaError {
            code: ResultCode::NotSupported as i32,
            message: format!("incompatible LRPA ABI version or structure layout ({version})"),
        })
    }
}

fn check(code: i32) -> Result<(), LrpaError> {
    if code == ResultCode::Ok as i32 {
        Ok(())
    } else {
        Err(LrpaError {
            code,
            message: last_error(),
        })
    }
}

fn last_error() -> String {
    let mut required = 0usize;
    unsafe {
        let _ = lrpa_last_error(ptr::null_mut(), 0, &mut required);
    }
    let mut bytes = vec![0u8; required.max(1)];
    let result = unsafe { lrpa_last_error(bytes.as_mut_ptr().cast(), bytes.len(), &mut required) };
    if result != ResultCode::Ok as i32 {
        return "LRPA operation failed".into();
    }
    unsafe { CStr::from_ptr(bytes.as_ptr().cast()) }
        .to_string_lossy()
        .into_owned()
}

unsafe extern "C" {
    fn lrpa_abi_version() -> u32;
    fn lrpa_options_size() -> usize;
    fn lrpa_input_size() -> usize;
    fn lrpa_mouse_report_size() -> usize;
    fn lrpa_open(options: *const RawOptions, out_handle: *mut usize, out_created: *mut i32) -> i32;
    fn lrpa_close(handle: usize) -> i32;
    fn lrpa_send_input(
        handle: usize,
        input_count: u32,
        inputs: *const Input,
        input_size: usize,
        out_sent: *mut u32,
    ) -> i32;
    fn lrpa_move(handle: usize, x: i32, y: i32) -> i32;
    fn lrpa_submit_report(handle: usize, report: *const MouseReport) -> i32;
    fn lrpa_last_error(buffer: *mut c_char, capacity: usize, out_required: *mut usize) -> i32;
}

const _: () = assert!(size_of::<RawOptions>() == 32);
const _: () = assert!(size_of::<Input>() == 40);
const _: () = assert!(size_of::<MouseReport>() == 8);

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn abi_layouts_match_version_one() {
        assert_eq!(size_of::<RawOptions>(), 32);
        assert_eq!(size_of::<Input>(), 40);
        assert_eq!(size_of::<MouseReport>(), 8);
    }
}
