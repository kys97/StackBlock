using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BlockUI : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler ,IDropHandler
{
    private string key;
    GameObject move_temp;
    private bool complete = false;

    public void SetKey(string n) { key = n; }

    public void OnBeginDrag(PointerEventData eventData)
    {
        //�巡�� ���� ���� key
        GameManager.Instance.drag_block_id = key;

        //moveImage ���� ����
        GameManager.Instance.move_canvas.SetActive(true);
        GameManager.Instance.move_ui_prefab.GetComponent<Image>().sprite = GameManager.Instance.Puzzle[key].ui;
        move_temp = Instantiate<GameObject>(GameManager.Instance.move_ui_prefab, GameManager.Instance.move_canvas.transform);
        move_temp.transform.position = transform.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        move_temp.transform.position = eventData.position;
        Debug.Log(Vector2.Distance(GameManager.Instance.Puzzle[key].position, move_temp.transform.position));
        if (Vector2.Distance(GameManager.Instance.Puzzle[key].position, move_temp.transform.position) <= GameManager.Instance.block_distance)
        {
            //��¦��
            complete = true;
        }
        else
        {
            //��¦�� �ڷ�ƾ ����
            complete = false;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (complete)
        {
            GameManager.Instance.Puzzle[key].block.SetActive(true);
            Destroy(move_temp.gameObject);
            move_temp = null;
            Destroy(gameObject);
        }
        else
        {
            Destroy(move_temp.gameObject);
            move_temp = null;
        }
    }

    public void OnDrop(PointerEventData eventData)
    {

    }

}
