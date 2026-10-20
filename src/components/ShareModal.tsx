import React, { useState } from 'react';
import { X, Copy, Check, Users, Globe } from 'lucide-react';
import { triggerConfetti } from '../utils/export';

interface ShareModalProps {
  isOpen: boolean;
  onClose: () => void;
  projectName: string;
}

export const ShareModal: React.FC<ShareModalProps> = ({ isOpen, onClose, projectName }) => {
  const [copied, setCopied] = useState(false);
  const [accessRole, setAccessRole] = useState<'edit' | 'view'>('edit');
  const [inviteEmail, setInviteEmail] = useState('');
  const [invitedList, setInvitedList] = useState<string[]>([
    'sarah.design@studio.io',
    'alex.lead@nexus.app',
  ]);

  if (!isOpen) return null;

  const shareUrl = `${window.location.origin}?project=${encodeURIComponent(
    projectName.toLowerCase().replace(/\s+/g, '-')
  )}`;

  const handleCopyLink = () => {
    navigator.clipboard.writeText(shareUrl);
    setCopied(true);
    triggerConfetti();
    setTimeout(() => setCopied(false), 2500);
  };

  const handleInvite = (e: React.FormEvent) => {
    e.preventDefault();
    if (inviteEmail.trim() && !invitedList.includes(inviteEmail.trim())) {
      setInvitedList([...invitedList, inviteEmail.trim()]);
      setInviteEmail('');
      triggerConfetti();
    }
  };

  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        backgroundColor: 'rgba(0, 0, 0, 0.75)',
        backdropFilter: 'blur(8px)',
        zIndex: 10000,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
      }}
      onClick={onClose}
    >
      <div
        style={{
          width: '500px',
          backgroundColor: 'var(--bg-panel)',
          border: '1px solid var(--border-color)',
          borderRadius: '16px',
          boxShadow: 'var(--shadow-lg)',
          overflow: 'hidden',
          display: 'flex',
          flexDirection: 'column',
        }}
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div
          style={{
            padding: '16px 20px',
            borderBottom: '1px solid var(--border-color)',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <Users size={18} color="var(--accent)" />
            <h3 style={{ fontSize: '15px', fontWeight: 600, color: '#fff' }}>
              Share "{projectName}"
            </h3>
          </div>
          <button className="figma-btn" onClick={onClose} style={{ padding: '4px' }}>
            <X size={16} />
          </button>
        </div>

        {/* Body */}
        <div style={{ padding: '20px', display: 'flex', flexDirection: 'column', gap: '16px' }}>
          {/* Invite Input */}
          <form onSubmit={handleInvite} style={{ display: 'flex', gap: '8px' }}>
            <input
              type="email"
              className="figma-input"
              placeholder="Email address, comma separated..."
              value={inviteEmail}
              onChange={(e) => setInviteEmail(e.target.value)}
              style={{ flex: 1, padding: '8px 12px', fontSize: '12px' }}
            />
            <select
              className="figma-input"
              value={accessRole}
              onChange={(e) => setAccessRole(e.target.value as any)}
              style={{ width: '100px' }}
            >
              <option value="edit">Can edit</option>
              <option value="view">Can view</option>
            </select>
            <button type="submit" className="figma-btn figma-btn-primary" style={{ padding: '0 16px' }}>
              Invite
            </button>
          </form>

          {/* Collaborator List */}
          <div>
            <div style={{ fontSize: '11px', fontWeight: 600, color: 'var(--text-secondary)', marginBottom: '8px' }}>
              COLLABORATORS WITH ACCESS
            </div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '6px' }}>
              {invitedList.map((email) => (
                <div
                  key={email}
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'space-between',
                    padding: '8px 12px',
                    background: 'var(--bg-panel-secondary)',
                    borderRadius: '6px',
                    border: '1px solid var(--border-subtle)',
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                    <div
                      style={{
                        width: '24px',
                        height: '24px',
                        borderRadius: '50%',
                        background: 'var(--accent)',
                        color: '#fff',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        fontSize: '11px',
                        fontWeight: 600,
                      }}
                    >
                      {email.charAt(0).toUpperCase()}
                    </div>
                    <span style={{ fontSize: '12px', color: 'var(--text-primary)' }}>{email}</span>
                  </div>
                  <span className="figma-badge">Can edit</span>
                </div>
              ))}
            </div>
          </div>

          {/* Copy Link Section */}
          <div
            style={{
              padding: '12px',
              background: 'var(--bg-input)',
              borderRadius: '8px',
              border: '1px solid var(--border-color)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
            }}
          >
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', overflow: 'hidden' }}>
              <Globe size={16} color="var(--text-secondary)" />
              <div style={{ display: 'flex', flexDirection: 'column' }}>
                <span style={{ fontSize: '12px', color: '#fff', fontWeight: 500 }}>
                  Anyone with the link can view
                </span>
                <span style={{ fontSize: '10px', color: 'var(--text-secondary)', textOverflow: 'ellipsis', overflow: 'hidden' }}>
                  {shareUrl}
                </span>
              </div>
            </div>

            <button
              className="figma-btn figma-btn-primary"
              onClick={handleCopyLink}
              style={{ whiteSpace: 'nowrap', padding: '6px 12px' }}
            >
              {copied ? <Check size={14} /> : <Copy size={14} />}
              <span>{copied ? 'Copied Link!' : 'Copy Link'}</span>
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
