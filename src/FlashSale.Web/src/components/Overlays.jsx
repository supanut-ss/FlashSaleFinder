import { useSession } from "../lib/session";
import { CloseIcon } from "./Icons";
import { Modal } from "./Modal";

export function NoticeDialog() {
  const { noticeOpen, confirmLogin, cancelLogin } = useSession();
  return (
    <Modal open={noticeOpen} onClose={cancelLogin} labelledBy="notice-title" className="modal-narrow">
      <div className="modal-top">
        <h2 id="notice-title" className="modal-title">เข้าสู่โหมดเดโม</h2>
        <button className="icon-button" type="button" onClick={cancelLogin} aria-label="ปิด"><CloseIcon /></button>
      </div>
      <div className="modal-body">
        <p className="panel-text">บัญชีเดโมนี้เป็นบัญชีตัวอย่างที่ใช้ร่วมกันบนเครื่องนี้ ไม่ใช่บัญชีผู้ใช้จริง</p>
        <div className="notice-actions">
          <button className="btn btn-primary" type="button" onClick={confirmLogin}>เข้าใช้บัญชีเดโม</button>
          <button className="btn btn-ghost" type="button" onClick={cancelLogin}>ไว้ก่อน</button>
        </div>
      </div>
    </Modal>
  );
}

export function Toast() {
  const { toast, dismissToast } = useSession();
  return (
    <div className="toast-region" role="status" aria-live="polite">
      {toast && (
        <div key={toast.id} className={`toast toast-${toast.tone}`}>
          <p>{toast.text}</p>
          <button type="button" onClick={dismissToast} aria-label="ปิดข้อความ"><CloseIcon /></button>
        </div>
      )}
    </div>
  );
}
