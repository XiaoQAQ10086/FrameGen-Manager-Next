use std::ffi::c_char;
use std::sync::atomic::{AtomicBool, AtomicUsize, Ordering};
use std::sync::{Arc, LazyLock, Mutex};

use fgm_infra::deployment::DeploymentProgress;
use serde_json::json;

use super::execute;

/// Current long-running operation, shared with the UI through the FFI.
pub(crate) struct OperationState {
    pub(crate) kind: String,
    pub(crate) cancelled: AtomicBool,
    done: AtomicUsize,
    total: AtomicUsize,
}

impl DeploymentProgress for OperationState {
    fn start(&self, total: usize) {
        self.total.store(total, Ordering::Relaxed);
        self.done.store(0, Ordering::Relaxed);
    }

    fn advance(&self) {
        self.done.fetch_add(1, Ordering::Relaxed);
    }
}

struct Registry {
    current: Mutex<Option<Arc<OperationState>>>,
}

static REGISTRY: LazyLock<Registry> = LazyLock::new(|| Registry {
    current: Mutex::new(None),
});

pub(crate) fn begin(kind: &str) -> Arc<OperationState> {
    let state = Arc::new(OperationState {
        kind: kind.to_string(),
        cancelled: AtomicBool::new(false),
        done: AtomicUsize::new(0),
        total: AtomicUsize::new(0),
    });
    if let Ok(mut current) = REGISTRY.current.lock() {
        *current = Some(state.clone());
    }
    state
}

pub(crate) fn finish() {
    if let Ok(mut current) = REGISTRY.current.lock() {
        *current = None;
    }
}

pub(crate) fn report_bytes(state: &OperationState, done: u64) {
    state.done.store(done as usize, Ordering::Relaxed);
}

pub(crate) fn report_total(state: &OperationState, total: u64) {
    state.total.store(total as usize, Ordering::Relaxed);
}

/// Returns `{"active":true,"kind":"deploy","done":3,"total":8,"cancelled":false}`.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_operation_progress_json() -> *mut c_char {
    execute(|| {
        let current = REGISTRY
            .current
            .lock()
            .ok()
            .and_then(|current| current.clone());
        match current {
            Some(state) => json!({
                "ok": true,
                "active": true,
                "kind": state.kind,
                "done": state.done.load(Ordering::Relaxed),
                "total": state.total.load(Ordering::Relaxed),
                "cancelled": state.cancelled.load(Ordering::Relaxed),
            }),
            None => json!({
                "ok": true,
                "active": false,
                "done": 0,
                "total": 0,
                "cancelled": false,
            }),
        }
    })
}

/// Requests cancellation of the current operation.
#[unsafe(no_mangle)]
pub extern "C" fn fgm_operation_cancel_json() -> *mut c_char {
    execute(|| {
        let current = REGISTRY
            .current
            .lock()
            .ok()
            .and_then(|current| current.as_ref().cloned());
        match current {
            Some(state) => {
                state.cancelled.store(true, Ordering::Relaxed);
                json!({ "ok": true, "cancelled": true })
            }
            None => json!({ "ok": false, "code": "NoActiveOperation" }),
        }
    })
}
